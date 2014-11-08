﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Optimization;
using System.Web.Routing;
using System.Threading;
using Civionics.DAL;
using Civionics.Models;

namespace Civionics
{
    public class MvcApplication : System.Web.HttpApplication
    {
        public const bool DEBUG = true;

        public const int START_DELAY = 60; //The delay (in seconds) to wait after application startup to launch the task threads
        public const int WATCH_PERIOD = 60; //How often the watch thread should check a folder (in seconds)
        public const int STATUS_PERIOD = 1; //How often the status thread should re-calculate project and sensor statuses (in hours)
        public const int PURGE_PERIOD = 24; //How often the server purges old data from the database (in hours)

        public const string WATCH_PATH = "/xls"; //The path to probe when watching

        public const int PURGE_OFFSET = 15; //Purge offset from today in days

        public const int SENSOR_WARNING_LEVEL = 1; //The amount of anomalous readings before a sensor has a warning
        public const int SENSOR_ALERT_LEVEL = 3; //The amount of anomalous readings before a sensor has an alert
        public const int SENSOR_WARNING_WEIGHT = 1; //The weight of a sensor warning considered when re-evaluating a project
        public const int SENSOR_ALERT_WEIGHT = 3; //The weight of a sensor alert considered when re-evaluating a project
        public const int PROJECT_WARNING_LEVEL = 3; //The amount of sensor weights before a project has a warning
        public const int PROJECT_ALERT_LEVEL = 9; //The amount of sensor weights before a project has an alert

        private static DateTime last;

        protected void Application_Start()
        {
            AreaRegistration.RegisterAllAreas();
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
            RouteConfig.RegisterRoutes(RouteTable.Routes);
            BundleConfig.RegisterBundles(BundleTable.Bundles);

            last = DateTime.Now;

            ThreadStart watch_task = new ThreadStart(watch_loop);
            ThreadStart status_task = new ThreadStart(status_loop);
            ThreadStart purge_task = new ThreadStart(purge_loop);

            Thread watcher = new Thread(watch_task);
            Thread statusupdater = new Thread(status_task);
            Thread purger = new Thread(purge_task);
            
            //watcher.Start();
            //statusupdater.Start();
            //purger.Start();

            System.Diagnostics.Debug.WriteLine("System ready...\n");
        }

        static void watch_loop()
        {
            if(DEBUG)
                System.Diagnostics.Debug.WriteLine("Watch thread active");
            System.Threading.Thread.Sleep(TimeSpan.FromSeconds(START_DELAY)); // Wait for the system to start

            for(;;)
            {
                touch_folder();
                System.Threading.Thread.Sleep(TimeSpan.FromSeconds(WATCH_PERIOD)); // Wait to loop
            }
        }

        static void status_loop()
        {
            if(DEBUG)
                System.Diagnostics.Debug.WriteLine("Status thread active");
            System.Threading.Thread.Sleep(TimeSpan.FromSeconds(START_DELAY + 5)); // Wait for the system to start

            for (;;)
            {
                calculate_status();
                System.Threading.Thread.Sleep(TimeSpan.FromHours(STATUS_PERIOD)); // Wait to loop
            }
        }

        static void purge_loop()
        {
            DateTime now = DateTime.Now;
            now.AddDays(-1 * PURGE_OFFSET);

            if (DEBUG)
                System.Diagnostics.Debug.WriteLine("Purge thread active");
            System.Threading.Thread.Sleep(TimeSpan.FromSeconds(START_DELAY + 10)); // Wait for the system to start

            for(;;)
            {
                purge();
                System.Threading.Thread.Sleep(TimeSpan.FromHours(PURGE_PERIOD)); //Wait to loop
            }
        }

        static void touch_folder()
        {
            if (DEBUG)
                System.Diagnostics.Debug.WriteLine("Touching folder: " + WATCH_PATH);

            //do shit here
        }

        static void calculate_status()
        {
            last = DateTime.Now;

            CivionicsContext db = new CivionicsContext();
            List<Project> projlist = db.Projects.ToList();

            for (int i = 0; i < projlist.Count; i++) // For each project
            {
                if(DEBUG)
                    System.Diagnostics.Debug.WriteLine("Project: " + projlist[i].ID);

                int projlev = 0;
                int totcount = 0;
                ProjectStatus status = ProjectStatus.Safe;
                List<Sensor> senslist = db.Sensors.Where(s => s.ProjectID == projlist[i].ID).ToList();

                for (int j = 0; j < senslist.Count; j++) // For each sensor in project
                {
                    if(DEBUG)
                        System.Diagnostics.Debug.WriteLine("\tSensor: " + senslist[j].ID);

                    int sencount = 0;
                    SensorStatus senstatus = SensorStatus.Safe;
                    List<Reading> readlist = db.Readings.Where(r => (r.SensorID == senslist[j].ID) && (r.LoggedTime > last)).OrderByDescending(r => r.LoggedTime).ToList();

                    for (int k = 0; k < readlist.Count; k++)
                    {
                        if(DEBUG)
                            System.Diagnostics.Debug.WriteLine("\t\tReading: " + readlist[k].ID + ", Date: " + readlist[k].LoggedTime);
                        if (readlist[k].isAnomalous)
                            sencount++;
                        totcount++;
                    }

                    projlev += sencount;
                    senstatus = (sencount > 0 ? (sencount > 2 ? SensorStatus.Alert : SensorStatus.Warning) : SensorStatus.Safe);
                    senslist[j].Status = senstatus;
                }

                int total = (projlev / totcount);
                status = total>=PROJECT_ALERT_LEVEL?ProjectStatus.Alert:(total>=PROJECT_WARNING_LEVEL?ProjectStatus.Warning:ProjectStatus.Safe);
                projlist[i].Status = status;
            }
            db.SaveChanges();
        }

        static void purge()
        {
            CivionicsContext db = new CivionicsContext();

            DateTime now = DateTime.Now;
            now.AddDays(-1 * PURGE_OFFSET);

            if(DEBUG)
                System.Diagnostics.Debug.WriteLine("Purging everything before: " + now.ToString() + "...");

            db.Readings.RemoveRange(db.Readings.Where(k => k.LoggedTime < now));

            db.SaveChanges();
        }
    }
}
