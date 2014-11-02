﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Data;
using System.Data.Entity;
using System.Net;
using Civionics.Models;
using Civionics.DAL;

namespace Civionics.Controllers
{
    public class SensorController : Controller
    {
        private CivionicsContext db = new CivionicsContext();

        //
        // GET: /Sensor/List
        public ActionResult List(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            return View(db.Sensors.Where(k => k.ProjectID == id).OrderByDescending(k => k.Status));
        }
	}
}