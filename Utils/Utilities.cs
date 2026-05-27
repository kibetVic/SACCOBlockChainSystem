using ClosedXML.Parser;
using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SACCOBlockChainSystem.Data;
using SACCOBlockChainSystem.Models;
using System.Net;
using System.Net.Sockets;

namespace SACCOBlockChainSystem.Utils
{
    
        public class GroupRightDTO
        {
            public string Feature { get; set; }
            public bool Value { get; set; }
        }

        //public class GroupRightDTO
        //{
        //    public string Feature { get; set; }
        //    public bool Value { get; set; }
        //}
        public enum AuditActionType
        {
            Insert,
            Update,
            Delete
        }
        //public enum Databases
        //{
        //    BOSA = "BOSA",
        //    MORINGA = "MORINGA",
        //    SLOPES = "SLOPES",
        //}
        //public class Securities
        //{
        //    private IDbContextFactory<LogContext> _context;
        //    //private LogContext _context;
        //    //public Securities(LogContext context)
        //    //{
        //    //    _context = context;
        //    //}

        //    public Securities(IDbContextFactory<LogContext> context)
        //    {
        //        _context = context;
        //    }
        //    public void SetUpPrivileges(Controller controller)
        //    {
        //    }

        //    public async Task SaveLogAsync(
        //        AuditActionType actionType,
        //        object? oldModel = null,
        //        object? newModel = null,
        //        string? tableName = null,
        //        string? recordId = null,
        //        string? userId = null,
        //        string? userName = null,
        //        string? companyCode = null,
        //        string? ipAddress = null,
        //        string? browserAgent = null,
        //        string? module = null,
        //        string? database = null,
        //        string? correlationId = null)
        //    {
        //        try
        //        {
        //            var audit = new AuditTrail
        //            {
        //                AuditTime = DateTime.UtcNow,
        //                ActionType = actionType.ToString(),
        //                TableName = tableName,
        //                RecordId = recordId,
        //                UserId = userId ?? "SYSTEM",
        //                //AuditId = userId,
        //                UserName = userName,
        //                CompanyCode = companyCode,
        //                IpAddress = ipAddress,
        //                BrowserAgent = browserAgent,
        //                Module = module,
        //                Database = database,
        //                CorrelationId = correlationId
        //            };

        //            switch (actionType)
        //            {
        //                case AuditActionType.Insert:
        //                    audit.ActionDescription = $"Record inserted into {tableName}";
        //                    audit.NewValue = newModel != null ? JsonSerializer.Serialize(newModel) : null;
        //                    break;

        //                case AuditActionType.Update:
        //                    audit.ActionDescription = $"Record updated in {tableName}";
        //                    audit.OldValue = oldModel != null ? JsonSerializer.Serialize(oldModel) : null;
        //                    audit.NewValue = newModel != null ? JsonSerializer.Serialize(newModel) : null;
        //                    break;

        //                case AuditActionType.Delete:
        //                    audit.ActionDescription = $"Record deleted from {tableName}";
        //                    audit.OldValue = oldModel != null ? JsonSerializer.Serialize(oldModel) : null;
        //                    break;
        //            }
        //            var context = await _context.CreateDbContextAsync();
        //            // Save asynchronously to database (non-blocking)
        //            await context.AuditTrail.AddAsync(audit);
        //            await context.SaveChangesAsync();

        //        }
        //        catch (Exception ex)
        //        {

        //        }

        //    }

        //    public static string GetLocalIPAddress()
        //    {
        //        var host = Dns.GetHostEntry(Dns.GetHostName());
        //        foreach (var ip in host.AddressList)
        //        {
        //            if (ip.AddressFamily == AddressFamily.InterNetwork)
        //            {
        //                return ip.ToString();
        //            }
        //        }
        //        return string.Empty;
        //        //throw new Exception("No network adapters with an IPv4 address in the system!");
        //    }

        //}


        public class Utilities
        {
            private ApplicationDbContext _context;
            public Utilities(ApplicationDbContext context)
            {
                _context = context;
            }
            public void SetUpPrivileges(Controller controller)
            {
                var sacco = controller.User.FindFirst("CompanyCode")?.Value ?? "MAIN";
                var Loggedinbranch =  controller.User.FindFirst("BranchCode")?.Value ?? "MAIN";
                var loggedInUser = controller.User.FindFirst("UserId")?.Value ?? "User";
                var group = controller.User.FindFirst("UserGroup")?.Value ?? "User";
                IQueryable<UserGroup> usergroupslist = _context.UserGroups;
                var usergroup = usergroupslist.FirstOrDefault(u => u.Name.Equals(group)
                );
                //create a default user group of admin for new society
                var company = new Company();
                company.CompanyCode = "SaccoBlock";
                company.Location = "KENYA";
                company.Email = "helpdesk@amtechafrica.com";
                if (usergroup == null)
                {
                    var val = new UserGroup
                    {
                        //Id= usergroup.Id,
                        //GroupId = group,
                        Name = group,
                        Description = "true",
                        CreatedBy = "system",

                        OrganizationCode = sacco,

                    };

                    _context.UserGroups.Add(val);
                    _context.SaveChanges();

                    usergroup = _context.UserGroups.FirstOrDefault(u => u.Name.Equals(group)
                           );
                }
                //end 
                //controller.ViewBag.slopes = StrValues.Slopes == sacco;
                //controller.ViewBag.Kabiyet = StrValues.Kabiyet == sacco;
                //controller.ViewBag.Kuresoi = StrValues.Kuresoi == sacco;
                //controller.ViewBag.elburgon = StrValues.Elburgon == sacco;
                //controller.ViewBag.Lelchego = StrValues.Lelchego == sacco;
                //controller.ViewBag.Emuka = StrValues.Emuka == sacco;
                //controller.ViewBag.Tulaga = StrValues.Tulaga == sacco;
                //controller.ViewBag.Mburugu = StrValues.Mburugu == sacco;
                //controller.ViewBag.Cherobu = StrValues.Cherobu == sacco;
                //controller.ViewBag.Soitaran = StrValues.Soitaran == sacco;

                //controller.ViewBag.CompPhone = StrValues.CompPhone == company.PhoneNo ?? 0;
                controller.ViewBag.isProject = false;


                var getuser = _context.UserAccounts1.FirstOrDefault(x => x.UserId == int.Parse(loggedInUser));
               

                controller.ViewBag.isLogged = controller.User.FindFirst("UserLoginId")?.Value == getuser.UserName;


                var rights = (
                    from u in _context.UserAccounts1
                    join rp in _context.RolePrivileges on u.GroupId equals rp.GroupId
                    join gr in _context.GroupRights on rp.RightId equals gr.RightId
                    where u.UserId == int.Parse(loggedInUser)
                    select new GroupRightDTO
                    {
                        Feature = gr.Feature,
                        Value = true
                    }
                ).Distinct().ToList();


                foreach (var right in rights)
                {
                    // Check if the key exists to avoid exceptions if a user has multiple roles with the same feature
                    if (!controller.ViewData.ContainsKey(right.Feature))
                    {
                        controller.ViewData.Add(right.Feature, right.Value);
                    }
                }
                controller.ViewBag.User = getuser.UserName;
                controller.ViewBag.Superuser = getuser.Superuser;
                controller.ViewBag.group = getuser.UserGroup;
                controller.ViewBag.Userid = getuser.UserId;
                controller.ViewBag.loggedInUser = getuser.UserName;
                controller.ViewBag.AccessLevel = 2;
                //controller.ViewBag.BranchLevel = AccessLevel.Branch;

                if (getuser.UserName == "User")
                {
                    controller.ViewBag.isLoggedIn = false;
                }
                else
                {
                    controller.ViewBag.isLoggedIn = true;
                }
                //
                var topics = new List<string>
            {
                "Support",
                "Enquiry",
                "Billing",
                "Sales",
                "Research & Dev",
                "General"
            };
                controller.ViewBag.HelpTopics = topics;
                var priority = new List<string> { "Emergency", "High", "Low", "Normal" };
                controller.ViewBag.Priorities = priority;
                controller.ViewBag.Company = sacco;
                controller.ViewBag.CompanyName = sacco.Substring(0, sacco.Length > 11 ? 11 : sacco.Length) + "..";
                controller.ViewBag.Loggedinbranch = Loggedinbranch;
                //foreach (var right in rights)
                //{

                //    controller.ViewData.Add(right.Feature,right.Value);
                //}

            }



            public string GetLocalIPAddress()
            {
                var host = Dns.GetHostEntry(Dns.GetHostName());
                foreach (var ip in host.AddressList)
                {
                    if (ip.AddressFamily == AddressFamily.InterNetwork)
                    {
                        return ip.ToString();
                    }
                }
                throw new Exception("No network adapters with an IPv4 address in the system!");
            }


        }
    
}
