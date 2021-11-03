using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using AirlineDB.Data;
using Domain.Entities;
using AirlineWeb.Shared.Interface;
using AirlineWeb.Shared.Models;
using AirlineWeb.Shared;
using Microsoft.AspNetCore.Identity;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using AirlineInterface;
using System.IO;
using System.Text;
using System.Web;

namespace AirlineAdmin.Controllers
{
    public class FlightLoader
    {
        public List<DateTime> FlightDates { get; set; }
        public List<FlightBWA> Flights { get; set; }
        public DateTime? curentDate { get; set; }
        public int? nextDate { get; set; }
        public int? previousDate { get; set; }
        public FlightSearchBWA FlightSearch { get; set; }
    }

    public class FlightLoaderController : Controller
    {
        private readonly IBWAService _service;
        private readonly ClientSettings _setting;
        private readonly IMemoryCache _cache;
        private FlightSearchBWA FlightSearch;
        private string name;

        public FlightLoaderController(BWAService service, ClientSettings setting, IMemoryCache cache)
        {
            _service = service;
            _setting = setting;
            _cache = cache;

        }


        // GET: FlightLoaderController
        public async Task<IActionResult> Index(int? currentDate)
        {
            name = User.Identity.Name;
            _setting.Encrypt(name);

            TravelAgentBWA travelAgent = new TravelAgentBWA();
            travelAgent.AgentNo = name;
            travelAgent.Settings = _setting;
            var dateContext = (_service.GetFlightDates(travelAgent)).Result;

            DateTime indexDate;
            try
            {
                indexDate = currentDate == null ? dateContext.FirstOrDefault() : dateContext.ElementAt((int)currentDate + 1);
            }
            catch (Exception ex)
            {
                indexDate = dateContext.OrderBy(dt => dt).FirstOrDefault();
            }

            _setting.Encrypt(name);
            travelAgent.AgentNo = name;
            travelAgent.Settings = _setting;
            travelAgent.FlightDate = indexDate;

            var airlineContext = (_service.GetFlights(travelAgent)).Result;
            var flightDates = dateContext;
            var flights = airlineContext.ToList();
            var flightLoader = new FlightLoader()
            {
                previousDate = dateContext.IndexOf(dateContext.Where(dt => dt.Date == indexDate).FirstOrDefault()) - 2,
                FlightDates = flightDates,
                Flights = flights,
                curentDate = indexDate,
                nextDate = dateContext.IndexOf(dateContext.Where(dt => dt.Date == indexDate).FirstOrDefault()),
            };
            return View(flightLoader);
        }

        // GET: Flights/Details/5
        [HttpGet("FlightLoad/Details/{id}/{flighton}")]
        public async Task<IActionResult> Details(Guid? id, string flighton)
        {
            if (id == null)
            {
                return NotFound();
            }

            TravelAgentBWA travelAgent = new TravelAgentBWA();
            travelAgent.AgentNo = name;
            travelAgent.Settings = _setting;
            travelAgent.FlightDate = DateTime.Parse(HttpUtility.UrlDecode(flighton)).Date;
            var flightContext = (_service.GetFlights(travelAgent)).Result;

            var flight = flightContext.Where(m => m.Id == id).FirstOrDefault();

            FlightSearchBWA search = new FlightSearchBWA();
            search.Origin = flight.Origin;
            search.Destination = flight.Destination;
            search.FlightDate = flight.FlightDate.Date;
            search.AdultCount = 1;
            search.ReturnFlightDate = flight.FlightDate.AddMonths(1).Date;
            return View(search);
        }

        // POST: FlightSearches/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Origin,Destination,FlightDate,ReturnFlightDate,RoundTrip,AdultCount")] FlightSearchBWA flightSearch)
        {
            FlightSearchBWA returnSearch = null;

            name = User.Identity.Name;
            _setting.Encrypt(name);

            returnSearch = await _service.SearchFlights(new FlightSearchBWA()
            {
                Origin = flightSearch.Origin,
                Destination = flightSearch.Destination,
                FlightDate = flightSearch.FlightDate,
                AdultCount = flightSearch.AdultCount,
                RoundTrip = flightSearch.RoundTrip,
                Settings = _setting
            });

            returnSearch.AdultCount = flightSearch.AdultCount;
            returnSearch.Origin = flightSearch.Origin;
            returnSearch.Destination = flightSearch.Destination;
            returnSearch.FlightDate = flightSearch.FlightDate;
            returnSearch.RoundTrip = flightSearch.RoundTrip;
            returnSearch.ReturnFlightDate = flightSearch.ReturnFlightDate;

            SetFlightSearchBWA(returnSearch.Id.ToString(), returnSearch);
            return RedirectToAction(nameof(HoldSeat), returnSearch);

        }

        public async Task<IActionResult> HoldSeat(FlightSearchBWA flightSearch)
        {
            FlightSearch = GetFlightSearchBWA(flightSearch.Id.ToString());
            //need to keep this for hold seat action
            //_cache.Remove(string.Format("FlightSearchBWA_{0}", flightSearch.Id));
            //string ssearch = JsonSerializer.Serialize(FlightSearch);

            //TempData["FlightSearch"] = ssearch;
            return View(FlightSearch);
        }

        [HttpGet]
        public async Task<string> SaveInventoryLinkDetail(string Id, string IVId, string FSId)
        {
            FlightSearch = GetFlightSearchBWA(FSId);
            var selectedSearchDetail = FlightSearch.SearchDetails.Where(sdt => sdt.Flight.InventoryLinks != null && sdt.Flight.InventoryLinks.Select(ivl => ivl.Id.ToString()).ToList().Contains(IVId)).FirstOrDefault();
            var fli = selectedSearchDetail.Flight.InventoryLinks.Where(fil => fil.Id.ToString() == IVId).First();
            selectedSearchDetail.BookingClass = fli.InventoryLinkDetails.Where(ild => ild.Id.ToString() == Id.ToString()).First().BookingClass;
            selectedSearchDetail.BookingCode = fli.InventoryLinkDetails.Where(ild => ild.Id.ToString() == Id.ToString()).First().BookingCode;
            SetFlightSearchBWA(FSId, FlightSearch);

            return selectedSearchDetail.Id.ToString();
        }

        [HttpGet]
        public async Task<string> RemoveInventoryLinkDetail(string Id, string FSId)
        {
            FlightSearch = GetFlightSearchBWA(FSId);
            var selectedSearchDetail = FlightSearch.SearchDetails.Where(sdt => sdt.Flight.InventoryLinks != null && sdt.Flight.InventoryLinks.Select(ivl => ivl.Id.ToString()).ToList().Contains(Id)).FirstOrDefault();
            var fli = selectedSearchDetail.Flight.InventoryLinks.Where(fil => fil.Id.ToString() == Id).First();
            selectedSearchDetail.BookingClass = "";
            selectedSearchDetail.BookingCode = "";
            SetFlightSearchBWA(FSId, FlightSearch);

            return selectedSearchDetail.Id.ToString();
        }

        [HttpPost]
        public async Task<IActionResult> ProcessHoldSeat([Bind("Id,SessionKey")] FlightSearch flightSearch)
        {
            FlightSearch = GetFlightSearchBWA(flightSearch.Id.ToString());

            var SearchDetails = new List<FlightSearchDetailBWA>();

            SearchDetails.AddRange(FlightSearch.SearchDetails.Where(srcd => srcd.BookingClass != "" && srcd.BookingCode != ""));

            FlightSearch.SearchDetails = SearchDetails;

            name = User.Identity.Name;
            _setting.Encrypt(name);
            FlightSearch.Settings = _setting;
            FlightSearch.SearchAgencyDetails = new List<FlightSearchAgencyDetailBWA>() { new FlightSearchAgencyDetailBWA() { AgentNo = name } };
            FlightSearch.Id = Guid.Empty;

            var HeldSearch = await _service.HoldSegments(FlightSearch);

            RemoveFlightSearchBWA(flightSearch.Id.ToString());

            var segs = HeldSearch.SearchDetails.SelectMany(sdt => sdt.HeldSegments).ToList();
            if (segs.Any())
            {
                SetHeldSearchBWA(HeldSearch.Id.ToString(), HeldSearch);
                return RedirectToAction(nameof(ReserveSeat), HeldSearch);
            }
            else
            {
                //redirect to error page
                return View(HeldSearch);
            }
        }

        public async Task<IActionResult> ReserveSeat(FlightSearchBWA heldSearch)
        {
            FlightSearch = GetHeldSearchBWA(heldSearch.Id.ToString());
            ViewData["HeldSearch"] = FlightSearch;

            if (FlightSearch.Passengers != null && FlightSearch.Passengers.Any())
                ViewData["Primary"] = false;
            else
                ViewData["Primary"] = true;

            ViewData["SalutationId"] = new SelectList(Enum.GetValues(typeof(ETitle)).Cast<ETitle>().Select(v => new SelectListItem
            {
                Text = v.ToString(),
                Value = ((int)v).ToString()
            }).ToList(), "Value", "Text");

            ViewData["PassengerTypeId"] = new SelectList(Enum.GetValues(typeof(EPassengerTypes)).Cast<EPassengerTypes>().Select(v => new
            {
                Text = v.ToString(),
                Value = ((int)v).ToString()
            }).ToList(), "Value", "Text");

            ViewData["StateId"] = new SelectList(Enum.GetValues(typeof(EStates)).Cast<EStates>().Select(v => new
            {
                Text = v.ToString(),
                Value = ((int)v).ToString()
            }).ToList(), "Value", "Text");

            ViewData["GenderId"] = new SelectList(Enum.GetValues(typeof(EGender)).Cast<EGender>().Select(v => new
            {
                Text = v.ToString(),
                Value = ((int)v).ToString()
            }).ToList(), "Value", "Text");

            return View(new PassengerBWA());
        }

        //AddPassenger
        [HttpPost]
        public async Task<IActionResult> AddPassenger([Bind("Uid,SalutationId,FirstName,LastName,BirthDate,GenderId,StreetName,ZipCode,Province,City,Area,PrimaryPhone,SecondaryPhone,Email,StateId,PromoCode,TripCount,PNRNumber,CustomerTypeId,TravelAgentId,PassengerTypeId,FlightSearchId,Id,CreatedBy,LastUpdated,CreatedAt,LastModifiedBy")] PassengerBWA passenger)
        {
            FlightSearch = GetHeldSearchBWA(passenger.Uid.ToString());

            passenger.Salutation = ((ETitle)passenger.SalutationId).ToString();
            passenger.PassengerType = new PassengerTypeBWA() { Name = ((EPassengerTypes)passenger.PassengerTypeId).ToString(), Index = passenger.PassengerTypeId };
            passenger.Gender = ((EGender)passenger.GenderId).ToString();

            if (FlightSearch.Passengers == null)
            {
                // this should be pulled from cache if not already
                var countries = _service.GetCountries().Result;
                // need to figure out how to do multi countries next time
                passenger.State = new StateBWA() { StateName = ((EStates)passenger.StateId).ToString(), Country = countries.Where(cnt => cnt.Index == 235).FirstOrDefault() };
                FlightSearch.Passengers = new List<PassengerBWA>() { passenger };
            }
            else
            {
                FlightSearch.Passengers.Add(passenger);
            }
            SetHeldSearchBWA(FlightSearch.Id.ToString(), FlightSearch);


            if (FlightSearch.Passengers.Count == FlightSearch.AdultCount + FlightSearch.ChildCount)
            {
                name = User.Identity.Name;
                _setting.Encrypt(name);
                FlightSearch.Settings = _setting;
                var returnSearch = await _service.ReserveSeats(FlightSearch);

                RemoveHeldSearchBWA(FlightSearch.Id.ToString());

                return RedirectToAction(nameof(Reservations));
            }
            else
            {
                return RedirectToAction(nameof(ReserveSeat), FlightSearch);
            }
        }

        public async Task<IActionResult> Reservations()
        {
            name = User.Identity.Name;
            _setting.Encrypt(name);

            TravelAgentBWA travelAgent = new TravelAgentBWA();
            travelAgent.AgentNo = name;
            travelAgent.Settings = _setting;
            var reservationContext = (_service.RefreshReservation(travelAgent)).Result;

            return View(reservationContext);
        }

        public async Task<IActionResult> CancelReservation(Guid id)
        {
            name = User.Identity.Name;
            _setting.Encrypt(name);

            TravelAgentBWA travelAgent = new TravelAgentBWA();
            travelAgent.AgentNo = name;
            travelAgent.Settings = _setting;

            var reservationContext = (_service.RefreshReservation(travelAgent)).Result;
            var res = reservationContext.Where(res => res.Id == id).FirstOrDefault();

            _setting.Encrypt(name);
            res.Settings = _setting;
            var resCancelled = (_service.CancelReservation(res)).Result;

            _setting.Encrypt(name);
            travelAgent.Settings = _setting;
            reservationContext = (_service.RefreshReservation(travelAgent)).Result;

            return RedirectToAction(nameof(Reservations));
        }

        public async Task<IActionResult> Tickets()
        {
            name = User.Identity.Name;
            _setting.Encrypt(name.ToString());

            TravelAgentBWA travelAgent = new TravelAgentBWA();
            travelAgent.AgentNo = name;
            travelAgent.Settings = _setting;
            var ticketContext = (_service.RefreshTicketing(travelAgent)).Result;

            return View(ticketContext);
        }

        public async Task<IActionResult> IssueTickets(Guid id)
        {
            name = User.Identity.Name;
            _setting.Encrypt(name);

            TravelAgentBWA travelAgent = new TravelAgentBWA();
            travelAgent.AgentNo = name;
            travelAgent.Settings = _setting;
            var reservationContext = (_service.RefreshReservation(travelAgent)).Result;

            var res = reservationContext.Where(res => res.Id == id).FirstOrDefault();

            _setting.Encrypt(name);
            res.Settings = _setting;
            var ticketing = (_service.IssueTicket(res)).Result;

            return RedirectToAction(nameof(Tickets));
        }

        public async Task<IActionResult> VoidTicket(Guid id)
        {
            name = User.Identity.Name;
            _setting.Encrypt(name);

            TravelAgentBWA travelAgent = new TravelAgentBWA();
            travelAgent.AgentNo = name;
            travelAgent.Settings = _setting;
            var ticketContext = (_service.RefreshTicketing(travelAgent)).Result;

            var ticket = ticketContext.SelectMany(tktng => tktng.Tickets).Where(tkt => tkt.Id == id).FirstOrDefault();
            _setting.Encrypt(name);
            ticket.Settings = _setting;

            var ticketing = (_service.VoidTicket(ticket)).Result;
            return RedirectToAction(nameof(Tickets));
        }

        public async Task<IActionResult> VoidTickets(Guid id)
        {
            name = User.Identity.Name;
            _setting.Encrypt(name);

            TravelAgentBWA travelAgent = new TravelAgentBWA();
            travelAgent.AgentNo = name;
            travelAgent.Settings = _setting;
            var ticketContext = (_service.RefreshTicketing(travelAgent)).Result;

            var ticketing = ticketContext.Where(tkting => tkting.Id == id).FirstOrDefault();
            _setting.Encrypt(name);
            ticketing.Settings = _setting;

            ticketing = (_service.VoidTickets(ticketing)).Result;

            return RedirectToAction(nameof(Tickets));
        }

        public async Task<IActionResult> RefundTicket(Guid id)
        {
            name = User.Identity.Name;
            _setting.Encrypt(name);

            TravelAgentBWA travelAgent = new TravelAgentBWA();
            travelAgent.AgentNo = name;
            travelAgent.Settings = _setting;
            var ticketContext = (_service.RefreshTicketing(travelAgent)).Result;

            var ticket = ticketContext.SelectMany(tktng => tktng.Tickets).Where(tkt => tkt.Id == id).FirstOrDefault();
            _setting.Encrypt(name);
            ticket.Settings = _setting;

            var ticketing = (_service.RefundTicket(ticket)).Result;

            return RedirectToAction(nameof(Tickets));
        }

        public async Task<IActionResult> RefundTickets(Guid id)
        {
            name = User.Identity.Name;
            _setting.Encrypt(name);

            TravelAgentBWA travelAgent = new TravelAgentBWA();
            travelAgent.AgentNo = name;
            travelAgent.Settings = _setting;
            var ticketContext = (_service.RefreshTicketing(travelAgent)).Result;

            var ticketing = ticketContext.Where(tkting => tkting.Id == id).FirstOrDefault();
            _setting.Encrypt(name);
            ticketing.Settings = _setting;

            ticketing = (_service.RefundTickets(ticketing)).Result;

            return RedirectToAction(nameof(Tickets));
        }

        public async Task<IActionResult> SplitPassenger(Guid id)
        {
            name = User.Identity.Name;
            _setting.Encrypt(name);

            TravelAgentBWA travelAgent = new TravelAgentBWA();
            travelAgent.AgentNo = name;
            travelAgent.Settings = _setting;
            var reservationContext = (_service.RefreshReservation(travelAgent)).Result;

            return View(reservationContext.Where(res => res.Id == id).FirstOrDefault().Passengers);
        }

        [HttpPost]
        public async Task<IActionResult> ProcessSplitPassenger(IList<PassengerBWA> splitPax)
        {
            name = User.Identity.Name;
            _setting.Encrypt(name);

            var splitPassenger = splitPax.Where(pax => pax.Split == true).Select(pax => pax.Id.ToString()).ToList();

            TravelAgentBWA travelAgent = new TravelAgentBWA();
            travelAgent.AgentNo = name;
            travelAgent.Settings = _setting;
            var reservationContext = (_service.RefreshReservation(travelAgent)).Result;
            var passengers = reservationContext.SelectMany(res => res.Passengers.Where(pax => splitPassenger.Contains(pax.Id.ToString())).ToList());

            var res = reservationContext.Where(res => res.Passengers.Contains(passengers.First())).FirstOrDefault();
            res.Passengers.ForEach(delegate (PassengerBWA pax) { if (splitPassenger.Contains(pax.Id.ToString())) { pax.Split = true; } });

            _setting.Encrypt(name);
            res.Settings = _setting;
            res = (_service.SplitPassenger(res)).Result;

            return RedirectToAction(nameof(Reservations));
        }

        public async Task<IActionResult> ExchangeTicket(Guid id)
        {
            name = User.Identity.Name;
            _setting.Encrypt(name);

            TravelAgentBWA travelAgent = new TravelAgentBWA();
            travelAgent.AgentNo = name;
            travelAgent.Settings = _setting;
            var ticketContext = (_service.RefreshTicketing(travelAgent)).Result;

            var res = ticketContext.First().Reservation;

            //return View(res.Segments.Select(seg => new SegmentHelper() { Id = seg.Id.ToString(), Destination = seg.Destination, Origin = seg.Origin, FlightDate = seg.FlightDate.ToShortDateString(), FlightNumber = seg.FlightNumber }).ToList());
            return View(res.Segments);
        }

        //ExchangeFlightSearch
        [HttpPost]
        public async Task<IActionResult> ExchangeFlightSearch(IList<ReservedSegmentBWA> hSegments)
        {

            //List<SegmentHelper> hSegments = new List<SegmentHelper>();

            name = User.Identity.Name;
            _setting.Encrypt(name);
            var exSegments = hSegments.Where(seg => seg.MarkedForExchangeNotNull == true).Select(seg => seg.Id).ToList();

            TravelAgentBWA travelAgent = new TravelAgentBWA();
            travelAgent.AgentNo = name;
            travelAgent.Settings = _setting;
            var reservationContext = (_service.RefreshReservation(travelAgent)).Result;
            var segments = reservationContext.Where(res => res.Segments != null).SelectMany(res => res.Segments).Where(seg => seg != null && exSegments.Contains(seg.Id)).ToList();
            var res = reservationContext.Where(res => res.Segments.Where(seg => segments.Select(sg => sg.Id).ToList().Contains(seg.Id)).Any()).FirstOrDefault();
            res.Segments.ForEach(delegate (ReservedSegmentBWA seg) { if (segments.Contains(seg)) { seg.MarkedForExchange = true; } });


            SetExchangeReservationBWA(name, res);

            return View(new FlightSearchBWA() { Origin = segments.First().Origin, Destination = segments.First().Destination, FlightDate = segments.First().FlightDate, ReturnFlightDate = segments.First().FlightDate.AddMonths(1), AdultCount = res.Passengers.Count() });
        }

        public async Task<IActionResult> ExchangeHoldSeat([Bind("Origin,Destination,FlightDate,ReturnFlightDate,RoundTrip,AdultCount")] FlightSearchBWA flightSearch)
        {
            name = User.Identity.Name;
            _setting.Encrypt(name);

            TravelAgentBWA travelAgent = new TravelAgentBWA();
            travelAgent.AgentNo = name;
            travelAgent.Settings = _setting;

            var res = GetExchangeReservationBWA(name);


            _setting.Encrypt(name);
            flightSearch.Settings = _setting;
            var returnSearch = (_service.SearchFlights(flightSearch)).Result;
            
            returnSearch.Origin = flightSearch.Origin;
            returnSearch.Destination = flightSearch.Destination;
            returnSearch.FlightDate = flightSearch.FlightDate;
            returnSearch.AdultCount = flightSearch.AdultCount;
            returnSearch.ReturnFlightDate = flightSearch.ReturnFlightDate;
            returnSearch.RoundTrip = flightSearch.RoundTrip;

            SetExchangeFlightSearchBWA(name, returnSearch);

            return View(returnSearch);
        }

        public async Task<IActionResult> VerifyExchange(Guid id)
        {

            name = User.Identity.Name;
            _setting.Encrypt(name);

            var res = GetExchangeReservationBWA(name);

            var flightSearch = GetExchangeFlightSearchBWA(name);
            res.ExchangeFlightSearch = flightSearch;

            return View(res);
        }

        public async Task<IActionResult> ProcessExchange()
        {
            name = User.Identity.Name;
            var res = GetExchangeReservationBWA(name);

            _setting.Encrypt(name);

            TravelAgentBWA travelAgent = new TravelAgentBWA();
            travelAgent.AgentNo = name;
            travelAgent.Settings = _setting;
            var ticketContext = (_service.RefreshTicketing(travelAgent)).Result;

            var ticketing = ticketContext.Where(tkting => tkting.Reservation.Id == res.Id).FirstOrDefault();

            ticketing.Reservation = res;

            var flightSearch = GetExchangeFlightSearchBWA(name);
            res.ExchangeFlightSearch = flightSearch;

            _setting.Encrypt(name);
            ticketing.Settings = _setting;

            flightSearch = (_service.HoldExchangeSegments(ticketing)).Result;
            ticketing.ExchangeHeldSearch = flightSearch;


            _setting.Encrypt(name);
            ticketing.Settings = _setting;
            ticketing = (_service.ExchangeTickets(ticketing)).Result;

            RemoveExchangeFlightSearchBWA(name);
            RemoveExchangeHeldSearchBWA(name);

            return RedirectToAction(nameof(Tickets));
        }

        [HttpGet]
        public async Task<string> SaveExchangeInventoryLinkDetail(string Id, string IVId, string FSId)
        {
            name = User.Identity.Name;
            FlightSearch = GetExchangeFlightSearchBWA(name);
            var selectedSearchDetail = FlightSearch.SearchDetails.Where(sdt => sdt.Flight.InventoryLinks != null && sdt.Flight.InventoryLinks.Select(ivl => ivl.Id.ToString()).ToList().Contains(IVId)).FirstOrDefault();
            var fli = selectedSearchDetail.Flight.InventoryLinks.Where(fil => fil.Id.ToString() == IVId).First();
            selectedSearchDetail.BookingClass = fli.InventoryLinkDetails.Where(ild => ild.Id.ToString() == Id.ToString()).First().BookingClass;
            selectedSearchDetail.BookingCode = fli.InventoryLinkDetails.Where(ild => ild.Id.ToString() == Id.ToString()).First().BookingCode;
            SetExchangeFlightSearchBWA(name, FlightSearch);

            return selectedSearchDetail.Id.ToString();
        }

        [HttpGet]
        public async Task<string> RemoveExchangeInventoryLinkDetail(string Id, string FSId)
        {
            name = User.Identity.Name;
            FlightSearch = GetExchangeFlightSearchBWA(name);
            var selectedSearchDetail = FlightSearch.SearchDetails.Where(sdt => sdt.Flight.InventoryLinks != null && sdt.Flight.InventoryLinks.Select(ivl => ivl.Id.ToString()).ToList().Contains(Id)).FirstOrDefault();
            var fli = selectedSearchDetail.Flight.InventoryLinks.Where(fil => fil.Id.ToString() == Id).First();
            selectedSearchDetail.BookingClass = "";
            selectedSearchDetail.BookingCode = "";
            SetExchangeFlightSearchBWA(name, FlightSearch);

            return selectedSearchDetail.Id.ToString();
        }

        private void SetExchangeFlightSearchBWA(string key, FlightSearchBWA search)
        {
            var cacheOptions = new MemoryCacheEntryOptions
            {
                AbsoluteExpiration = DateTime.Now.AddMinutes(15),
                Priority = CacheItemPriority.High,
                SlidingExpiration = TimeSpan.FromMinutes(5),
                Size = 1024,
            };
            _cache.Set(string.Format("ExchangeFlightSearchBWA_{0}", key), search, cacheOptions);
        }

        private FlightSearchBWA GetExchangeFlightSearchBWA(string key)
        {
            return _cache.Get<FlightSearchBWA>(string.Format("ExchangeFlightSearchBWA_{0}", key));
        }

        private void SetExchangeReservationBWA(string key, ReservationBWA reservation)
        {
            var cacheOptions = new MemoryCacheEntryOptions
            {
                AbsoluteExpiration = DateTime.Now.AddMinutes(15),
                Priority = CacheItemPriority.High,
                SlidingExpiration = TimeSpan.FromMinutes(5),
                Size = 1024,
            };
            _cache.Set(string.Format("ExchangeReservationBWA_{0}", key), reservation, cacheOptions);
        }

        private ReservationBWA GetExchangeReservationBWA(string key)
        {
            return _cache.Get<ReservationBWA>(string.Format("ExchangeReservationBWA_{0}", key));
        }

        //HeldSearchBWA_
        private void SetHeldSearchBWA(string key, FlightSearchBWA search)
        {
            var cacheOptions = new MemoryCacheEntryOptions
            {
                AbsoluteExpiration = DateTime.Now.AddMinutes(15),
                Priority = CacheItemPriority.High,
                SlidingExpiration = TimeSpan.FromMinutes(5),
                Size = 1024,
            };
            _cache.Set(string.Format("HeldSearchBWA_{0}", key), search, cacheOptions);
        }

        private FlightSearchBWA GetHeldSearchBWA(string key)
        {
            return _cache.Get<FlightSearchBWA>(string.Format("HeldSearchBWA_{0}", key));
        }


        private void SetFlightSearchBWA(string key, FlightSearchBWA search)
        {
            _cache.Set(string.Format("FlightSearchBWA_{0}", key), search);
        }

        private FlightSearchBWA GetFlightSearchBWA(string key)
        {
            return _cache.Get<FlightSearchBWA>(string.Format("FlightSearchBWA_{0}", key));
        }

        private void RemoveFlightSearchBWA(string key)
        {
            _cache.Remove(string.Format("FlightSearchBWA_{0}", key));
        }

        private void RemoveHeldSearchBWA(string key)
        {
            _cache.Remove(string.Format("HeldSearchBWA_{0}", key));
        }

        private void RemoveExchangeFlightSearchBWA(string key)
        {
            _cache.Remove(string.Format("ExchangeHeldSearchBWA_{0}", key));
        }
        private void RemoveExchangeHeldSearchBWA(string key)
        {
            _cache.Remove(string.Format("ExchangeHeldSearchBWA_{0}", key));
        }
    }

}
