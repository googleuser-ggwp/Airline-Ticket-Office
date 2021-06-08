﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using AirLineTicketOffice.Logic;
using AirLineTicketOffice.Model;
using AirLineTicketOffice.View;
using static AirLineTicketOffice.Logic.Service_class;

namespace AirLineTicketOffice.ViewModels
{
    public partial class FlightsViewModel:INotifyPropertyChanged
    {
        private RelayCommand search;
        private RelayCommand getFilterCommand;
        private RelayCommand buy;
        private RelayCommand resetCommand;
        private RelayCommand addFlight;
        private RelayCommand removeFLight;
        

        public RelayCommand RemoveFlightCommand
        {
            get
            {
                return removeFLight ??
                       (removeFLight = new RelayCommand(o =>
                       {
                           FlightVariant flight=o as FlightVariant;
                           if (flight!=null)
                           {
                               Flights.Remove(flight);
                               //TODO Deleting from db
                           }
                       },
                        o=>Flights.Count()!=0  ));
            }
        }
        
        public RelayCommand AddFlightCommand
        {
            get
            {
                return addFlight ??
                       (addFlight = new RelayCommand(o =>
                       {
                           FlightVariant newFlight = new FlightVariant();
                           filteredFlights.Insert(0,newFlight);
                           Flights.Insert(0,newFlight);
                           SelectedFlight = newFlight;
                           
                           //TODO add_to_db
                       }));
            }
        }
        
        public RelayCommand ResetCommand
        {
            get
            {
                return resetCommand ??
                       (resetCommand = new RelayCommand(o =>
                       {
                           Filter=new Filter();
                           filteredFlights = oldState;
                       },
                           o=> Filter.DepartureTimeSelected || Filter.ArrivalTimeSelected || Filter.PriceSelected));
            }
        }
        
        public RelayCommand GetFilterCommand
        {
            get
            {
                return getFilterCommand ??
                       (getFilterCommand = new RelayCommand(o =>
                       {
                           ApplyFilter();
                       },
                           o=>Filter.DepartureTimeSelected  ||
                              Filter.ArrivalTimeSelected || Filter.PriceSelected));
            }
        }
        
        public RelayCommand BuyCommand 
        {
            get
            {
                return buy ??
                       (buy = new RelayCommand(o =>
                           {
                               ToggleButton currentButton = o as ToggleButton;
                               if (!ApplicationViewModel.isAuthorized)
                               {
                                   Authorization authWindow = new Authorization();
                                   authWindow.Owner = Application.Current.MainWindow;
                                   if (authWindow.ShowDialog() == true)
                                   {
                                       
                                   }
                                   currentButton.IsChecked = false;
                                   return;
                                   
                               }

                               if(!ApplicationViewModel.isAuthorized)
                               {
                                   currentButton.IsChecked = false;
                                   return;
                               }
                              
                               if(CurrentUser.IsAdmin)
                               {
                                   MessageBox.Show("Для покупки войдите в свой личный акканут","",MessageBoxButton.OK, MessageBoxImage.Information);
                               }
                               else //TODO kakaja-to fignja
                               {
                                   Passenger currentPassenger = MainWindow.db.Passengers.First(u=>u.name==CurrentUser.Name && u.surname==CurrentUser.Surname);
                                   Airline currentAirline = Airlines
                                       .First(u => SelectedFlight.CompanyName==u.company_name);
                                   
                                   
                                   //TODO ne tot bilet
                                   Flight currentFlight=currentAirline.Flights.First(
                                       u=>u.departure_city==SelectedFlight.DepartureCity && u.arrival_city==SelectedFlight.ArrivalCity);

                                   List<Place> places = _Places.Where(u=>currentFlight==u.Flight).Select(u=>u).ToList();

                                   var TicketsAndPlaces = _Places.Join(Tickets,
                                       p => p.Place_ID,
                                       t => t.Place_ID,
                                       ((place, ticket) =>
                                           new
                                           {
                                                ticket.Ticket_ID,
                                                ticket.Place_ID,
                                                ticket.Place,
                                                ticket.Passenger,
                                                ticket.departure_date,
                                                ticket.departure_time,
                                                ticket.isCanceled,
                                                ticket.isExpired
                                           }));

                                   //генерация билетов, если их нет
                                   if ( TicketsAndPlaces.Count() == 0)
                                   {
                                       List<Ticket> tickets = new List<Ticket>();
                                       for (int i = 0; i < places.Count(); i++)
                                       {
                                           tickets.Add(new Ticket
                                           {
                                               Place = places[i],
                                               Passenger = null,
                                               departure_date = DepartureDay,
                                               departure_time = SelectedFlight.DepartureTime,
                                               isCanceled = "No",
                                               isExpired = "No"
                                           });
                                       }
                                       
                                       tickets[0].Passenger = currentPassenger;
                                       
                                       MainWindow.db.Tickets.AddRange(tickets);
                                       MainWindow.db.SaveChanges();
                                   }
                                   else //если билеты уже сгенерированны
                                   {
                                       var freeTicketID=TicketsAndPlaces.Where(u => u.Passenger == null).Select(u=>u.Ticket_ID).First();
                                       Ticket freeTicket=MainWindow.db.Tickets.First(u=>u.Ticket_ID==freeTicketID);
                                       currentPassenger.Tickets.Add(freeTicket);
                                       MainWindow.db.SaveChanges();
                                   }

                                   currentButton.IsChecked = false;
                                   MessageBox.Show("\u2714  Билет успешно приобретён");
                               }
                         
                           }
                       ));
            }
        }
        
        public RelayCommand Search 
        {
            get
            {
                return search ?? (search = new RelayCommand(o =>
                    {
                        string serviceClass = "";
                        switch (ServiceClass)
                        {
                            case (int)Economy:
                                serviceClass = "Economy";
                                break;
                            
                            case (int)Business:
                                serviceClass = "Business";
                                break;
                        }

                        var foundFlights = Flights.Where(obj =>
                            obj.DepartureCity == DepartureCity &&
                            obj.ArrivalCity == ArrivalCity &&
                            obj.DepartureDay == DepartureDate.DayOfWeek.ToString() &&
                            obj.ServiceClass == serviceClass).Select(obj => obj);

                        filteredFlights.Clear();
                        
                        var iterator = foundFlights.GetEnumerator();

                        while (iterator.MoveNext())
                        {
                            filteredFlights.Add( new FlightVariant
                            {
                                CompanyName = iterator.Current.CompanyName,
                                DepartureCity = iterator.Current.DepartureCity,
                                ArrivalCity = iterator.Current.ArrivalCity,
                                DepartureDay = iterator.Current.DepartureDay,
                                DepartureTime = iterator.Current.DepartureTime,
                                ArrivalDay = iterator.Current.ArrivalDay,
                                ArrivalTime = iterator.Current.ArrivalTime,
                                ServiceClass = iterator.Current.ServiceClass,
                                Price = iterator.Current.Price
                            });
                        }
                    },
        (o => (DepartureCity != "") && (ArrivalCity != "")
                )));
            }
        }
        
        
        public void ApplyFilter()
        {
            ObservableCollection<FlightVariant> filteringResult = new ObservableCollection<FlightVariant>();
            List<FlightVariant> flightVariants = filteredFlights.ToList();
            
            
            if (Filter.DepartureTimeSelected)
            {
                var filterByDepartureTime = flightVariants.Where(u=>u.DepartureTime< new TimeSpan(Filter.Departure_time,0 ,0)).Select(u=>u);
                flightVariants= new List<FlightVariant>();
                flightVariants.AddRange(filterByDepartureTime);
            }

            if (Filter.ArrivalTimeSelected)
            {
                var filterByArrivalTime = flightVariants.Where(u=>u.ArrivalTime< new TimeSpan(Filter.Arrival_time,0 ,0));
                flightVariants= new List<FlightVariant>();
                flightVariants.AddRange(filterByArrivalTime);
            }

            if (Filter.PriceSelected)
            {
                var filterByPrice = flightVariants.Where(u=>u.Price< Filter.Price1);
                flightVariants= new List<FlightVariant>();
                flightVariants.AddRange(filterByPrice);
            }

            
            filteringResult= new ObservableCollection<FlightVariant>(flightVariants);
            oldState = filteredFlights;
            filteredFlights = filteringResult;
        }


        void CheckPassengerTicket(Passenger passenger)
        {
            
        }
        
        
    }
}