using Microsoft.AspNetCore.Mvc;
using Cleanup.Models;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Cleanup
{
    public class CleanupController : Controller //Controller for Cleanup CRUD
    {
        private CleanupContext _context;
        public CleanupController(CleanupContext context)
        {
            _context = context;
        }
        //message test
        [HttpGet]
        [Route("mboard/{id}")]
        public IActionResult MBoard(int id){
            int? activeId = HttpContext.Session.GetInt32("activeUser");
            if(activeId != null) //Checked to make sure user is actually logged in
            {  
                //retrive the messages by event with id and INCLUDE boardmessages;
                ViewBag.messages = _context.boardmessages.Where(c => c.EventId == id).OrderBy(c => c.CreatedAt).Include(m => m.Sender).ToList();
                ViewBag.cleanup = _context.cleanups.Single(e => e.CleanupId == id);       
                return View("mboard");
            }
            return RedirectToAction("Index", "User");
        }

        [HttpGet]
        [Route("dashboard")] //Needs a legit Route
        public IActionResult Dashboard()
        {
            int? activeId = HttpContext.Session.GetInt32("activeUser");
            if(activeId != null) //Checked to make sure user is actually logged in
            {
                //getting all the events
                var events = _context.cleanups.Where(c => c.Pending == false).Include(c => c.CleaningUsers).Include(c => c.User).ToList();
                ViewBag.markers = events;
                User active = _context.users.Single(u => u.UserId == activeId);
                ViewBag.active = active; 
                return View("Dashboard");
            }
            return RedirectToAction("Index", "User");
        }
        [HttpPost]
        [Route("postboardmessage/{id}")]
        public IActionResult PostBoardMessage(int id, string content){
            int? activeId = HttpContext.Session.GetInt32("activeUser");
            if(activeId != null) //Checked to make sure user is actually logged in
            {   
                if (content == null){
                    ViewBag.error = "Content can't be empty";
                    ViewBag.messages = _context.boardmessages.Where(c => c.EventId == id).OrderBy(c => c.CreatedAt).Include(m => m.Sender).ToList();
                    ViewBag.cleanup = _context.cleanups.Single(e => e.CleanupId == id);      
                    return View("mboard");
                }
                BoardMessage bm = new BoardMessage{
                    SenderId = (int)HttpContext.Session.GetInt32("activeUser"),
                    EventId = id,
                    Content = content
                };
                _context.Add(bm);
                _context.SaveChanges();
                return RedirectToAction("MBoard");
            }
            return RedirectToAction("Index", "User");
        }
        [HttpGet]
        [Route("add/cleanup")]
        public IActionResult NewCleanup()
        {
            int? activeId = HttpContext.Session.GetInt32("activeUser");
            if(activeId != null) //Checked to make sure user is actually logged in
            {
                return View();
            }
            return RedirectToAction("Index", "User");
        }
        [HttpPost]
        [Route("add/cleanup")]
        public IActionResult AddCleanup(CleanupViewModel model)
        {
            int? activeId = HttpContext.Session.GetInt32("activeUser");
            if(activeId != null) //Checked to make sure user is actually logged in
            {
                User activeUser = _context.users.Single( u => u.UserId == (int)activeId);
                if(activeUser.Token>0)
                {
                    if(ModelState.IsValid)
                    {
                        CleanupEvent newCleanup = new CleanupEvent{
                            Title = model.Title,
                            DescriptionOfArea = model.DescriptionOfArea,
                            DescriptionOfTrash = model.DescriptionOfTrash,
                            UserId = (int)activeId,
                            Pending = true,
                            Value = 0,
                            MaxCleaners = 0,
                            Address = model.Address,
                            Latitude = model.Latitude,
                            Longitude = model.Longitude
                        };
                        _context.Add(newCleanup);
                        activeUser.Token-=1;
                        _context.SaveChanges();
                        CleanupEvent freshCleanup = _context.cleanups.OrderBy( c => c.CreatedAt ).Reverse().First();
                        return RedirectToAction("AddPhoto", new { id = freshCleanup.CleanupId});
                    }
                }
                else
                {
                    ViewBag.error = "Insufficient tokens to report trash, go and help out more!";
                }
                return View("NewCleanup");
            }
            return RedirectToAction("Index", "User");
        }
        [HttpGet]
        [Route("cleanup/{id}")]
        public IActionResult ViewCleanup(int id)
        {
            int? activeId = HttpContext.Session.GetInt32("activeUser");
            if(activeId != null) //Checked to make sure user is actually logged in
            {
                List<CleanupEvent> possibleCleanup = _context.cleanups.Where( c => c.CleanupId == id).Include( c => c.Images ).Include( c => c.CleaningUsers).ToList();
                if(possibleCleanup.Count == 1)
                {
                    ViewBag.viewedCleanup = possibleCleanup[0];
                    return View();
                }
            }
            return RedirectToAction("Index", "User");
        }
        [HttpGet]
        [Route("delete/cleanup/{id}")]
        public IActionResult DeleteCleanup(int id)
        {
            int? activeId = HttpContext.Session.GetInt32("activeUser");
            if(activeId != null) //Checked to make sure user is actually logged in
            {
                User activeUser = _context.users.Single( u => u.UserId == (int)activeId);
                List<CleanupEvent> possibleCleanup = _context.cleanups.Where( c => c.CleanupId == id).ToList();
                if(possibleCleanup.Count == 1)
                {
                    if(activeUser.UserLevel == 9 || (possibleCleanup[0].Pending = true && possibleCleanup[0].UserId == activeUser.UserId))
                    {
                        _context.cleanups.Remove(possibleCleanup[0]);
                        _context.SaveChanges();
                        return RedirectToAction("Dashboard");
                    }
                }
            }
            return RedirectToAction("Index", "User");
        }
        [HttpPost]
        [Route("approve/cleanup/{id}")]
        public IActionResult ApproveCleanup(int id, int value, int max)
        {
            int? activeId = HttpContext.Session.GetInt32("activeUser");
            if(activeId != null) //Checked to make sure user is actually logged in
            {
                User activeUser = _context.users.Single( u => u.UserId == (int)activeId);
                List<CleanupEvent> possibleCleanup = _context.cleanups.Where( c => c.CleanupId == id).ToList();
                if(possibleCleanup.Count == 1 && activeUser.UserLevel == 9) //Confirm that event exists and that user is admin
                {
                    possibleCleanup[0].Pending = false;
                    possibleCleanup[0].MaxCleaners = max;
                    possibleCleanup[0].Value = value;
                    _context.SaveChanges();
                    return RedirectToAction("Dashboard");
                }
            }
            return RedirectToAction("Index", "User");
        }
        [HttpGet]
        [Route("close/cleanup/{id}")]
        public IActionResult CloseCleanup(int id)
        {
            int? activeId = HttpContext.Session.GetInt32("activeUser");
            if(activeId != null) //Checked to make sure user is actually logged in
            {
                User activeUser = _context.users.Single( u => u.UserId == (int)activeId);
                List<CleanupEvent> possibleCleanup = _context.cleanups.Where( c => c.CleanupId == id).Include( c => c.CleaningUsers ).ToList();
                if(possibleCleanup.Count == 1 && activeUser.UserLevel == 9) //Confirm that event exists and that user is admin
                {
                    int scoreEarned = (possibleCleanup[0].Value/possibleCleanup[0].CleaningUsers.Count);
                    foreach(User cleaninguser in possibleCleanup[0].CleaningUsers)
                    {
                        cleaninguser.Score = scoreEarned;
                        cleaninguser.Token += 1;
                        return RedirectToAction("DeleteCleanup", new { id = possibleCleanup[0].CleanupId});
                    }
                }
            }
            return RedirectToAction("Index", "User");
        }
        [HttpGet]
        [Route("add/photos/cleanup/{id}")]
        public IActionResult AddPhoto(int id)
        {
            int? activeId = HttpContext.Session.GetInt32("activeUser");
            if(activeId != null) //Checked to make sure user is actually logged in
            {
                List<CleanupEvent> possibleCleanup = _context.cleanups.Where( c => c.CleanupId == id).Include( c => c.Images ).ToList();
                if(possibleCleanup.Count == 1)
                {
                    ViewBag.Cleanup = possibleCleanup[0];
                    return View();
                }
            }
            return RedirectToAction("Index", "User");
        }
        [HttpPost]
        [Route("add/photos/cleanup/{id}")]
        public IActionResult ProcessPhoto(int id)
        {
            int? activeId = HttpContext.Session.GetInt32("activeUser");
            if(activeId != null) //Checked to make sure user is actually logged in
            {
                List<CleanupEvent> possibleCleanup = _context.cleanups.Where( c => c.CleanupId == id).ToList();
                if(possibleCleanup.Count == 1 && possibleCleanup[0].UserId == (int)activeId)//Confirm that they went to an existing cleanup event and that they should be the one adding photos
                {
                    //Code to change photo filename, ERIC LOOK HERE
                    return RedirectToAction("AddPhoto", new { id = possibleCleanup[0].CleanupId}); //After new photo added, redirect to photo add page so user can add more (up to 5 max)
                }
            }
            return RedirectToAction("Index", "User");
        }
        [HttpGet]
        [Route("admin/page")]
        public IActionResult AdminPage()
        {
            int? activeId = HttpContext.Session.GetInt32("activeUser");
            if(activeId != null)
            {
                User activeUser = _context.users.Single( u => u.UserId == (int)activeId);
                if(activeUser.UserLevel == 9)
                {
                    ViewBag.allCleanups = _context.cleanups.Include( c => c.User ).Include( u => u.Images ).OrderBy( l => l.UpdatedAt ).ToList();
                    return View();
                }
            }
            return RedirectToAction("Index", "User");
        }
        [HttpGet]
        [Route("admin/cleanup/{id}")]
        public IActionResult AdminCleanupPage(int id)
        {
            int? activeId = HttpContext.Session.GetInt32("activeUser");
            if(activeId != null)
            {
                User activeUser = _context.users.Single( u => u.UserId == (int)activeId);
                List<CleanupEvent> possibleCleanup = _context.cleanups.Where( c => c.CleanupId == id).Include( c => c.Images ).Include( c => c.User ).ToList();
                if(activeUser.UserLevel == 9 && possibleCleanup.Count == 1 && possibleCleanup[0].Pending == true)
                {
                    ViewBag.cleanup = possibleCleanup[0];
                    return View();
                }
            }
            return RedirectToAction("Index", "User");
        }
        [HttpGet]
        [Route("decline/cleanup/{id}")]
        public IActionResult DeclineCleanupReport(int id)
        {
            int? activeId = HttpContext.Session.GetInt32("activeUser");
            if(activeId != null) //Checked to make sure user is actually logged in
            {
                User activeUser = _context.users.Single( u => u.UserId == (int)activeId);
                List<CleanupEvent> possibleCleanup = _context.cleanups.Where( c => c.CleanupId == id).ToList();
                if(possibleCleanup.Count == 1 && activeUser.UserLevel == 9 && possibleCleanup[0].Pending == true) //Confirm that event exists and that user is admin
                {
                    possibleCleanup[0].Pending = false;
                    _context.SaveChanges();
                }
            }
            return RedirectToAction("Index", "User");
        }
    }
}