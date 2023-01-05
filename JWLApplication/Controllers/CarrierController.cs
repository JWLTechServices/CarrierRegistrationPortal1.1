using AutoMapper;
using Data;
using Interfaces;
using JWLApplication.Filters;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore.Internal;
using Microsoft.Extensions.Configuration;
using Models;
using OfficeOpenXml;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Mail;
using System.Threading.Tasks;
using iTextSharp.text;
using iTextSharp.text.html.simpleparser;
using iTextSharp.text.pdf;
using Newtonsoft.Json;
using EPPlus.DataExtractor;
using OfficeOpenXml.FormulaParsing.Excel.Functions.Information;
using System.Text.RegularExpressions;

namespace JWLApplication.Controllers
{
    [ServiceFilter(typeof(MyActionFilterAttribute))]
    public class CarrierController : Controller
    {
        private readonly ICarrierService _carrierService;
        private readonly IStateService _stateService;
        private readonly ICityService _cityService;
        private readonly IMarkeSstateService _markeSstateService;
        private readonly IVehicleTypeService _vehicleTypeService;
        private readonly ICargoService _cargoService;
        private readonly IPaymentService _paymentService;
        private readonly ITrailerService _trailerService;
        private readonly IWebHostEnvironment _hostingEnvironment;
        private readonly GoogleRecaptchaService _googleRecaptchaService;
        private readonly IMapper _mapper;
        private readonly ICarrierProcessingService _carrierProcessingService;
        string LogoUrl = "";
        private static Mailclient MailDetails;
        private static RecaptchaSettings RecaptchaSettings;
        IConfigurationRoot configuration;
        private readonly JWLDBContext _jWLDBContext;
        public CarrierController(ICarrierService carrierService, IStateService stateService
            , ICityService cityService, IMarkeSstateService markeSstateService
            , IVehicleTypeService vehicleTypeService, ICargoService cargoService
            , IPaymentService paymentService, IWebHostEnvironment hostingEnvironment
            , GoogleRecaptchaService googleRecaptchaService, ITrailerService trailerService
            , IMapper mapper, ICarrierProcessingService carrierProcessingService, JWLDBContext jWLDBContext)
        {
            _carrierService = carrierService;
            _stateService = stateService;
            _cityService = cityService;
            _markeSstateService = markeSstateService;
            _vehicleTypeService = vehicleTypeService;
            _cargoService = cargoService;
            _paymentService = paymentService;
            _hostingEnvironment = hostingEnvironment;
            _trailerService = trailerService;
            _mapper = mapper;
            _carrierProcessingService = carrierProcessingService;
            LogoUrl = "http://jwl.credencys.net:9089/img/jwl-logo.png";
            _googleRecaptchaService = googleRecaptchaService;
            configuration = new ConfigurationBuilder()
              .SetBasePath(Directory.GetCurrentDirectory())
              .AddJsonFile("appsettings.json")
              .Build();

            MailDetails = configuration.GetSection("MailClient").Get<Mailclient>();
            RecaptchaSettings = configuration.GetSection("RecaptchaSettings").Get<RecaptchaSettings>();
            _jWLDBContext = jWLDBContext;
        }

        // GET: CarrierController/Create
        public async Task<ActionResult> Create()
        {
            HttpContext.Session.Clear();
            ViewData["States"] = new SelectList(await _stateService.GetActiveStates(), "stateId", "stateName");
            ViewData["TrailerType"] = new SelectList(await _trailerService.GetActiveTrailer(), "trailerId", "trailerName");
            ViewData["FactoryStates"] = new SelectList(await _stateService.GetActiveStates(), "stateId", "stateName");
            ViewData["MarketStates"] = new SelectList(await _stateService.GetActiveStates(), "stateId", "stateName");
            ViewData["TrailerType"] = new SelectList(await _trailerService.GetActiveTrailer(), "trailerId", "trailerName");
            ViewData["VehicleTypes"] = new SelectList(await _vehicleTypeService.GetActiveVehicleTypes(), "vehicleId", "vehicleName");
            ViewData["CargoSpecialities"] = new SelectList(await _cargoService.GetActiveCargoSpecialities(), "cargoId", "cargoName");
            ViewData["PaymentTypes"] = new SelectList(await _paymentService.GetActivePaymentTypes(), "paymentTypeId", "paymentName");
            return View(new carrierusers());
        }
        // POST: CarrierController/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        ////public async Task<ActionResult> Create(carrierusers carrierUser)
        public async Task<ActionResult> Create([Bind("cuName,cuEmail,agreementDate,MC,authorizedPerson,title,legalCompanyName,DBA,physicalAddress,city,state,zipcode,telephone,fax,factoryCompanyName,factoryContactName,factoryPhysicalAddress,factoryCity,factoryState,factoryZipCode,factoryTelephone,factoryFax,additionalPersonName,addtionalPersonTelephone,addtionalAfterHoursPersonName,addtionalAfterHoursPersonTelephone,addtionalDot,additionalScac,additionalFedaralID,additionalHazmatCertified,additinalHazmatExpiryDate,additionalPreferredLanes,additionalMajorMakets,authorizedSignaturePath,authorizedSignature,authorizedDocuments,majorMarkets,isMinorityBusiness,businessYear,ethnicIdentification,milesPerYear,cargoSpecification,paymentMethods,twic,CarrierwiseVehicle,CarrierDocument,ca,isFemaleOwned,isVeteranOwned,Token,serviceArea,brokerOptions,CarrierwiseTrailer")] carrierusers carrierUser)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    var googlerecaptcha = await _googleRecaptchaService.Verification(carrierUser.Token);
                    if (googlerecaptcha.success && googlerecaptcha.score >= 0.5)
                    {
                        // To check the cuEmail is already exist
                        var CheckEmail = await _carrierService.CheckEmail(carrierUser.cuEmail, false, 0);
                        if (CheckEmail == "True")
                        {
                            ModelState.AddModelError("cuEmail", "Email already Exists");
                        }
                        else
                        {
                            carrierUser.status = StatusEnum.New;
                            carrierUser.createdByUserName = carrierUser.authorizedPerson;
                            await _carrierService.AddCarrierUser(carrierUser, HttpContext.Session.GetString("UserID"));
                            await SendMail(carrierUser.authorizedPerson, carrierUser.cuEmail);
                            //Generate PDF link
                            MemoryStream ms = new MemoryStream();
                            string FileLocation = $@"{_hostingEnvironment.WebRootPath}/\PDFTemplate\/JWLNDA.htm";
                            var htmlString = string.Join("", System.IO.File.ReadAllLines(FileLocation));
                            //var htmlString = $@"{_hostingEnvironment.WebRootPath}/\PDFTemplate\/JWLNDA.htm";
                            string compayName = string.Empty;
                            if (carrierUser.legalCompanyName != null)
                            {
                                compayName = carrierUser.legalCompanyName;
                            }
                            else if (carrierUser.DBA != null)
                                compayName = carrierUser.DBA;
                            else
                            {
                                compayName = @"<br/><span style='color:#ccc;text-decoration: underline;'>Enter Carrier/ Broker Company NameType a message</span>";
                            }
                            var jwlNDAhtml = htmlString.Replace("##DATE##", DateTime.Now.ToString("MM/dd/yyyy")).Replace("##NAME##", carrierUser.authorizedPerson).Replace("##ADDRESS##", carrierUser.physicalAddress).
                                Replace("##COMPANYNAME##", compayName);
                            var jwNDAPDF = GetPDF(jwlNDAhtml);
                            ms = new MemoryStream(jwNDAPDF);

                            string fileNameJWLNDA = $@"/\PDFAttachment/\_{carrierUser.cuId}_{DateTime.Now.ToString("MMddyyyy")}_{"JWLNDA.pdf"}";
                            string FilePath2 = _hostingEnvironment.WebRootPath + fileNameJWLNDA;
                            using (FileStream file = new FileStream(FilePath2, FileMode.Create, FileAccess.Write))
                            {
                                ms.WriteTo(file);
                                file.Close();
                            }
                            TempData["JWLNDAPDF"] = this.Request.Scheme + @"://" + this.Request.Host + this.Request.PathBase + fileNameJWLNDA;
                            string FileLocation2 = $@"{_hostingEnvironment.WebRootPath}/\PDFTemplate\/MasterBrokerCarrier.html";
                            var MasterBrokerCarrierHTML = string.Join("", System.IO.File.ReadAllLines(FileLocation2));
                            string space = new string(' ', 3);
                            var MasterBrokerCarrierPDF2 = MasterBrokerCarrierHTML.Replace("##DATE##", DateTime.Now.ToString("MM/dd/yyyy")).Replace("##NAME##", carrierUser.authorizedPerson).Replace("##MC##", carrierUser != null ? carrierUser.MC : space).Replace("##DOT##", carrierUser.addtionalDot).Replace("##FEDERALTAXID##", carrierUser.additionalFedaralID);

                            var MasterBrokerCarrierPDF = GetPDF(MasterBrokerCarrierPDF2);
                            ms = new MemoryStream(MasterBrokerCarrierPDF);

                            string fileMasterBrokerCarrier = $@"/\PDFAttachment/\_{carrierUser.cuId}_{DateTime.Now.ToString("MMddyyyy")}_{"MasterBrokerCarrier.pdf"}";
                            string FilePath3 = _hostingEnvironment.WebRootPath + fileMasterBrokerCarrier;

                            using (FileStream file = new FileStream(FilePath3, FileMode.Create, FileAccess.Write))
                            {
                                ms.WriteTo(file);
                                file.Close();
                                ms.Close();
                            }
                            TempData["MasterBrokerCarrierPDF"] = this.Request.Scheme + @"://" + this.Request.Host + this.Request.PathBase + fileMasterBrokerCarrier;
                            TempData.Keep("JWLNDAPDF");
                            TempData.Keep("MasterBrokerCarrierPDF");
                            carrierUser.ndaUrl = this.Request.Scheme + @"://" + this.Request.Host + this.Request.PathBase + fileNameJWLNDA;
                            carrierUser.mbcaUrl = this.Request.Scheme + @"://" + this.Request.Host + this.Request.PathBase + fileMasterBrokerCarrier;
                            await _carrierService.UpdateURL(carrierUser, "");

                            return RedirectToAction(nameof(Thankyou));
                        }
                    }
                    else
                    {
                        if (string.IsNullOrEmpty(carrierUser.Token))
                        {
                            ModelState.AddModelError("Token", "Invalid reCAPTCHA, Please close this window, Reopen the window and try again.");
                        }
                        else if (googlerecaptcha.score <= 0.5)
                        {
                            ModelState.AddModelError("Token", "Unauthorized Request, Error:40103, Please try again.");
                        }
                        else
                        {
                            ModelState.AddModelError("Token", "reCAPTCHA verification failed, Please try again.");
                        }
                    }
                }
                ViewData["States"] = new SelectList(await _stateService.GetActiveStates(), "stateId", "stateName", carrierUser.state);
                ViewData["FactoryStates"] = new SelectList(await _stateService.GetActiveStates(), "stateId", "stateName", carrierUser.factoryState);
                ViewData["MarketStates"] = new SelectList(await _stateService.GetActiveStates(), "stateId", "stateName", carrierUser.majorMarkets);
                ViewData["TrailerType"] = new SelectList(await _trailerService.GetActiveTrailer(), "trailerId", "trailerName", carrierUser.CarrierwiseTrailer);
                ViewData["VehicleTypes"] = new SelectList(await _vehicleTypeService.GetActiveVehicleTypes(), "vehicleId", "vehicleName", carrierUser.CarrierwiseVehicle);
                ViewData["CargoSpecialities"] = new SelectList(await _cargoService.GetActiveCargoSpecialities(), "cargoId", "cargoName", carrierUser.cargoSpecification);
                ViewData["PaymentTypes"] = new SelectList(await _paymentService.GetActivePaymentTypes(), "paymentTypeId", "paymentName", carrierUser.paymentMethods);
                return View(carrierUser);
            }
            catch (Exception ex)
            {
                ViewData["States"] = new SelectList(await _stateService.GetActiveStates(), "stateId", "stateName", carrierUser.state);
                ViewData["FactoryStates"] = new SelectList(await _stateService.GetActiveStates(), "stateId", "stateName", carrierUser.factoryState);
                ViewData["MarketStates"] = new SelectList(await _stateService.GetActiveStates(), "stateId", "stateName", carrierUser.majorMarkets);
                ViewData["TrailerType"] = new SelectList(await _trailerService.GetActiveTrailer(), "trailerId", "trailerName", carrierUser.CarrierwiseTrailer);
                ViewData["VehicleTypes"] = new SelectList(await _vehicleTypeService.GetActiveVehicleTypes(), "vehicleId", "vehicleName", carrierUser.CarrierwiseVehicle);
                ViewData["CargoSpecialities"] = new SelectList(await _cargoService.GetActiveCargoSpecialities(), "cargoId", "cargoName", carrierUser.cargoSpecification);
                ViewData["PaymentTypes"] = new SelectList(await _paymentService.GetActivePaymentTypes(), "paymentTypeId", "paymentName", carrierUser.paymentMethods);
                return View(carrierUser);
            }
        }

        public byte[] GetPDF(string pHTML)
        {
            byte[] bPDF = null;

            MemoryStream ms = new MemoryStream();
            TextReader txtReader = new StringReader(pHTML);
            Document doc = new Document(PageSize.A4);
            PdfWriter oPdfWriter = PdfWriter.GetInstance(doc, ms);
            HTMLWorker htmlWorker = new HTMLWorker(doc);
            doc.Open();
            htmlWorker.StartDocument();
            htmlWorker.Parse(txtReader);
            htmlWorker.EndDocument();
            htmlWorker.Close();
            doc.Close();
            bPDF = ms.ToArray();
            return bPDF;
        }
        // GET: Carrier/Register
        public async Task<ActionResult> Register()
        {
            if (HttpContext.Session.GetString("UserID") == null || HttpContext.Session.GetString("UserID") == "")
            {
                return RedirectToAction("Login", "Users");
            }
            bool isActive = await _carrierProcessingService.IsActive(Convert.ToInt32(HttpContext.Session.GetString("UserID")), "Carrier/Register");
            if (!isActive)
            {
                return RedirectToAction("Login", "Users");
            }
            ViewData["States"] = new SelectList(await _stateService.GetActiveStates(), "stateId", "stateName");
            ViewData["FactoryStates"] = new SelectList(await _stateService.GetActiveStates(), "stateId", "stateName");
            ViewData["MarketStates"] = new SelectList(await _stateService.GetActiveStates(), "stateId", "stateName");
            ViewData["TrailerType"] = new SelectList(await _trailerService.GetActiveTrailer(), "trailerId", "trailerName");
            ViewData["VehicleTypes"] = new SelectList(await _vehicleTypeService.GetActiveVehicleTypes(), "vehicleId", "vehicleName");
            ViewData["CargoSpecialities"] = new SelectList(await _cargoService.GetActiveCargoSpecialities(), "cargoId", "cargoName");
            ViewData["PaymentTypes"] = new SelectList(await _paymentService.GetActivePaymentTypes(), "paymentTypeId", "paymentName");
            return View(new carrierusers() { status = StatusEnum.New });
        }

        // POST: Carrier/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Register([Bind("cuId,cuName,cuEmail,agreementDate,MC,authorizedPerson,title,legalCompanyName,DBA,physicalAddress,city,state,zipcode,telephone,fax,factoryCompanyName,factoryContactName,factoryPhysicalAddress,factoryCity,factoryState,factoryZipCode,factoryTelephone,factoryFax,additionalPersonName,addtionalPersonTelephone,addtionalAfterHoursPersonName,addtionalAfterHoursPersonTelephone,addtionalDot,additionalScac,additionalFedaralID,additionalHazmatCertified,additinalHazmatExpiryDate,additionalPreferredLanes,additionalMajorMakets,authorizedSignaturePath,authorizedSignature,authorizedDocuments,majorMarkets,isMinorityBusiness,businessYear,ethnicIdentification,milesPerYear,cargoSpecification,paymentMethods,twic,CarrierwiseVehicle,CarrierDocument,completedMBCA,reportSortKey,ndaReturned,onboardingCompleted,onboardingFileLink,insuranceType,paymentTerms,CreatedDate,createdBy,ca,isFemaleOwned,isVeteranOwned,status,serviceArea,brokerOptions,CarrierwiseTrailer,Ins1MGeneral,Ins1MAuto")] carrierusers carrierUser)
        {
            try
            {
                bool isActive = await _carrierProcessingService.IsActive(Convert.ToInt32(HttpContext.Session.GetString("UserID")), "Carrier/Register");
                if (!isActive)
                {
                    return RedirectToAction("Login", "Users");
                }
                if (ModelState.IsValid)
                {
                    if (carrierUser.insuranceType == "Ins100KCargo")
                    {
                        carrierUser.Ins100KCargo = true;
                    }
                    if (carrierUser.insuranceType == "Ins250KCargo")
                    {
                        carrierUser.Ins250KCargo = true;
                    }
                    carrierUser.createdBy = carrierUser.modifiedBy = carrierUser.assignee = Convert.ToInt32(HttpContext.Session.GetString("UserID"));
                    carrierUser.createdByUserName = HttpContext.Session.GetString("UserFullName");
                    carrierUser.status = StatusEnum.Inprocess;
                    await _carrierService.AddCarrierUser(carrierUser, HttpContext.Session.GetString("UserID"));
                    SmtpClient client = new SmtpClient(MailDetails.SmtpServer, MailDetails.Port);
                    client.Credentials = new NetworkCredential(MailDetails.UserName, MailDetails.Password);
                    client.EnableSsl = true;
                    MailMessage message = new MailMessage();
                    message.From = new MailAddress(MailDetails.Sender);
                    message.To.Add(new MailAddress(carrierUser.cuEmail));
                    message.Subject = "Registration Request Received";
                    message.IsBodyHtml = true; //to make message body as html  
                    string RegisterFile = $@"{_hostingEnvironment.WebRootPath}/\MailTemplate\/Registration.html";
                    var RegisterMailHTML = string.Join("", System.IO.File.ReadAllLines(RegisterFile));
                    RegisterMailHTML = RegisterMailHTML.Replace("##Date##", DateTime.Now.ToString("MMM dd, yyyy")).Replace("##Person##", carrierUser.authorizedPerson);
                    message.Body = RegisterMailHTML;
                    await client.SendMailAsync(message);

                    SmtpClient clients = new SmtpClient(MailDetails.SmtpServer, MailDetails.Port);
                    clients.Credentials = new NetworkCredential(MailDetails.UserName, MailDetails.Password);
                    clients.EnableSsl = true;
                    MailMessage messages = new MailMessage();
                    messages.From = new MailAddress(MailDetails.Sender);
                    messages.To.Add(new MailAddress(carrierUser.cuEmail));
                    messages.IsBodyHtml = true; //to make message body as html  
                    if (carrierUser.status == StatusEnum.Inprocess)
                    {

                        messages.Subject = "Onboarding Started";
                        string OnboardingFile = $@"{_hostingEnvironment.WebRootPath}/\MailTemplate\/Onboarding.html";
                        var UserHTML = string.Join("", System.IO.File.ReadAllLines(OnboardingFile));
                        UserHTML = UserHTML.Replace("##Date##", DateTime.Now.ToString("MMM dd, yyyy")).Replace("##Name##", carrierUser.authorizedPerson);
                        messages.Body = UserHTML;
                        await clients.SendMailAsync(messages);
                    }
                    //Generate PDF link
                    MemoryStream ms = new MemoryStream();
                    string FileLocation = $@"{_hostingEnvironment.WebRootPath}/\PDFTemplate\/JWLNDA.htm";
                    var htmlString = string.Join("", System.IO.File.ReadAllLines(FileLocation));
                    //var htmlString = $@"{_hostingEnvironment.WebRootPath}/\PDFTemplate\/JWLNDA.htm";
                    string compayName = string.Empty;
                    if (carrierUser.legalCompanyName != null)
                    {
                        compayName = carrierUser.legalCompanyName;
                    }
                    else if (carrierUser.DBA != null)
                        compayName = carrierUser.DBA;
                    else
                    {
                        compayName = @"<br/><span style='color:#ccc;text-decoration: underline;'>Enter Carrier/ Broker Company NameType a message</span>";
                    }
                    var jwlNDAhtml = htmlString.Replace("##DATE##", DateTime.Now.ToString("MM/dd/yyyy")).Replace("##NAME##", carrierUser.authorizedPerson).Replace("##ADDRESS##", carrierUser.physicalAddress).
                        Replace("##COMPANYNAME##", compayName);
                    var jwNDAPDF = GetPDF(jwlNDAhtml);
                    ms = new MemoryStream(jwNDAPDF);

                    string fileNameJWLNDA = $@"/\PDFAttachment/\_{carrierUser.cuId}_{DateTime.Now.ToString("MMddyyyy")}_{"JWLNDA.pdf"}";
                    string FilePath2 = _hostingEnvironment.WebRootPath + fileNameJWLNDA;
                    using (FileStream file = new FileStream(FilePath2, FileMode.Create, FileAccess.Write))
                    {
                        ms.WriteTo(file);
                        file.Close();
                    }
                    TempData["JWLNDAPDF"] = this.Request.Scheme + @"://" + this.Request.Host + this.Request.PathBase + fileNameJWLNDA;
                    string FileLocation2 = $@"{_hostingEnvironment.WebRootPath}/\PDFTemplate\/MasterBrokerCarrier.html";
                    var MasterBrokerCarrierHTML = string.Join("", System.IO.File.ReadAllLines(FileLocation2));
                    string space = new string(' ', 3);
                    var MasterBrokerCarrierPDF2 = MasterBrokerCarrierHTML.Replace("##DATE##", DateTime.Now.ToString("MM/dd/yyyy")).Replace("##NAME##", carrierUser.authorizedPerson).Replace("##MC##", carrierUser != null ? carrierUser.MC : space).Replace("##DOT##", carrierUser.addtionalDot).Replace("##FEDERALTAXID##", carrierUser.additionalFedaralID);

                    var MasterBrokerCarrierPDF = GetPDF(MasterBrokerCarrierPDF2);
                    ms = new MemoryStream(MasterBrokerCarrierPDF);

                    string fileMasterBrokerCarrier = $@"/\PDFAttachment/\_{carrierUser.cuId}_{DateTime.Now.ToString("MMddyyyy")}_{"MasterBrokerCarrier.pdf"}";
                    string FilePath3 = _hostingEnvironment.WebRootPath + fileMasterBrokerCarrier;

                    using (FileStream file = new FileStream(FilePath3, FileMode.Create, FileAccess.Write))
                    {
                        ms.WriteTo(file);
                        file.Close();
                        ms.Close();
                    }
                    TempData["MasterBrokerCarrierPDF"] = this.Request.Scheme + @"://" + this.Request.Host + this.Request.PathBase + fileMasterBrokerCarrier;
                    TempData.Keep("JWLNDAPDF");
                    TempData.Keep("MasterBrokerCarrierPDF");
                    carrierUser.ndaUrl = this.Request.Scheme + @"://" + this.Request.Host + this.Request.PathBase + fileNameJWLNDA;
                    carrierUser.mbcaUrl = this.Request.Scheme + @"://" + this.Request.Host + this.Request.PathBase + fileMasterBrokerCarrier;
                    await _carrierService.UpdateURL(carrierUser, HttpContext.Session.GetString("UserID"));
                    return RedirectToAction(nameof(ManageCarriers));
                }
                ViewData["States"] = new SelectList(await _stateService.GetActiveStates(), "stateId", "stateName", carrierUser.state);
                ViewData["FactoryStates"] = new SelectList(await _stateService.GetActiveStates(), "stateId", "stateName", carrierUser.factoryState);
                ViewData["MarketStates"] = new SelectList(await _stateService.GetActiveStates(), "stateId", "stateName", carrierUser.majorMarkets);
                ViewData["TrailerType"] = new SelectList(await _trailerService.GetActiveTrailer(), "trailerId", "trailerName", carrierUser.CarrierwiseTrailer);
                ViewData["VehicleTypes"] = new SelectList(await _vehicleTypeService.GetActiveVehicleTypes(), "vehicleId", "vehicleName", carrierUser.CarrierwiseVehicle);
                ViewData["CargoSpecialities"] = new SelectList(await _cargoService.GetActiveCargoSpecialities(), "cargoId", "cargoName", carrierUser.cargoSpecification);
                ViewData["PaymentTypes"] = new SelectList(await _paymentService.GetActivePaymentTypes(), "paymentTypeId", "paymentName", carrierUser.paymentMethods);
                return View(carrierUser);
            }
            catch (Exception ex)
            {
                ViewData["States"] = new SelectList(await _stateService.GetActiveStates(), "stateId", "stateName", carrierUser.state);
                ViewData["FactoryStates"] = new SelectList(await _stateService.GetActiveStates(), "stateId", "stateName", carrierUser.factoryState);
                ViewData["MarketStates"] = new SelectList(await _stateService.GetActiveStates(), "stateId", "stateName", carrierUser.majorMarkets);
                ViewData["TrailerType"] = new SelectList(await _trailerService.GetActiveTrailer(), "trailerId", "trailerName", carrierUser.CarrierwiseTrailer);
                ViewData["VehicleTypes"] = new SelectList(await _vehicleTypeService.GetActiveVehicleTypes(), "vehicleId", "vehicleName", carrierUser.CarrierwiseVehicle);
                ViewData["CargoSpecialities"] = new SelectList(await _cargoService.GetActiveCargoSpecialities(), "cargoId", "cargoName", carrierUser.cargoSpecification);
                ViewData["PaymentTypes"] = new SelectList(await _paymentService.GetActivePaymentTypes(), "paymentTypeId", "paymentName", carrierUser.paymentMethods);
                return RedirectToAction("ManageCarriers", "Carrier");
            }
        }

        public ActionResult Thankyou()
        {
            return View();
        }
        // GET: CarrierController/Edit/5
        public async Task<ActionResult> Edit(int? id)
        {
            if (HttpContext.Session.GetString("UserID") == null || HttpContext.Session.GetString("UserID") == "")
            {
                return RedirectToAction("Login", "Users");
            }
            bool isActive = await _carrierProcessingService.IsActive(Convert.ToInt32(HttpContext.Session.GetString("UserID")), "Carrier/Edit" + id);
            if (!isActive)
            {
                return RedirectToAction("Login", "Users");
            }
            if (id == null)
            {
                return NotFound();
            }
            carrierusers carrierusers = await _carrierService.GetCarrierusersById(id.Value);
            if (carrierusers == null)
            {
                return NotFound();
            }
            TempData["JWLNDAPDF"] = "";
            TempData["MasterBrokerCarrierPDF"] = "";
            ViewData["States"] = new SelectList(await _stateService.GetActiveStates(), "stateId", "stateName", carrierusers.state);
            ViewData["FactoryStates"] = new SelectList(await _stateService.GetActiveStates(), "stateId", "stateName", carrierusers.factoryState);
            ViewData["MarketStates"] = new SelectList(await _stateService.GetActiveStates(), "stateId", "stateName", carrierusers.majorMarkets);
            ViewData["TrailerType"] = new SelectList(await _trailerService.GetActiveTrailer(), "trailerId", "trailerName", carrierusers.CarrierwiseTrailer);
            ViewData["VehicleTypes"] = new SelectList(await _vehicleTypeService.GetActiveVehicleTypes(), "vehicleId", "vehicleName", carrierusers.CarrierwiseVehicle);
            ViewData["CargoSpecialities"] = new SelectList(await _cargoService.GetActiveCargoSpecialities(), "cargoId", "cargoName", carrierusers.cargoSpecification);
            ViewData["PaymentTypes"] = new SelectList(await _paymentService.GetActivePaymentTypes(), "paymentTypeId", "paymentName", carrierusers.paymentMethods);
            return View(carrierusers);
        }
        // GET: Carrier/ManageCarriers
        public async Task<ActionResult> ManageCarriers()
        {
            if (HttpContext.Session.GetString("UserID") == null || HttpContext.Session.GetString("UserID") == "")
            {
                return RedirectToAction("Login", "Users");
            }
            bool isActive = await _carrierProcessingService.IsActive(Convert.ToInt32(HttpContext.Session.GetString("UserID")), "Carrier/ManageCarriers");
            if (!isActive)
            {
                return RedirectToAction("Login", "Users");
            }
            return View(await _carrierService.GetCarrierUsers());
        }
        // POST: CarrierController/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Edit(int id, [Bind("cuId,cuName,cuEmail,agreementDate,MC,authorizedPerson,title,legalCompanyName,DBA,physicalAddress,city,state,zipcode,telephone,fax,factoryCompanyName,factoryContactName,factoryPhysicalAddress,factoryCity,factoryState,factoryZipCode,factoryTelephone,factoryFax,additionalPersonName,addtionalPersonTelephone,addtionalAfterHoursPersonName,addtionalAfterHoursPersonTelephone,addtionalDot,additionalScac,additionalFedaralID,additionalHazmatCertified,additinalHazmatExpiryDate,additionalPreferredLanes,additionalMajorMakets,authorizedSignaturePath,authorizedSignature,authorizedDocuments,majorMarkets,isMinorityBusiness,businessYear,ethnicIdentification,milesPerYear,cargoSpecification,paymentMethods,twic,CarrierwiseVehicle,CarrierDocument,completedMBCA,reportSortKey,ndaReturned,onboardingCompleted,onboardingFileLink,insuranceType,paymentTerms,CreatedDate,createdBy,ca,isFemaleOwned,isVeteranOwned,status,assignee,serviceArea,brokerOptions,CarrierwiseTrailer,createdByUserName,Ins1MGeneral,Ins1MAuto")] carrierusers carrierUser)
        {
            try
            {
                bool isActive = await _carrierProcessingService.IsActive(Convert.ToInt32(HttpContext.Session.GetString("UserID")), "Carrier/Register");
                if (!isActive)
                {
                    return RedirectToAction("Login", "Users");
                }
                if (ModelState.IsValid)
                {
                    if (carrierUser.insuranceType == "Ins100KCargo")
                    {
                        carrierUser.Ins100KCargo = true;
                    }
                    if (carrierUser.insuranceType == "Ins250KCargo")
                    {
                        carrierUser.Ins250KCargo = true;
                    }
                    carrierUser.modifiedBy = Convert.ToInt32(HttpContext.Session.GetString("UserID"));
                    await _carrierService.EditCarrierUser(carrierUser, HttpContext.Session.GetString("UserID"));

                    return RedirectToAction("ManageCarriers");
                }
                ViewData["States"] = new SelectList(await _stateService.GetActiveStates(), "stateId", "stateName", carrierUser.state);
                ViewData["FactoryStates"] = new SelectList(await _stateService.GetActiveStates(), "stateId", "stateName", carrierUser.factoryState);
                ViewData["MarketStates"] = new SelectList(await _stateService.GetActiveStates(), "stateId", "stateName", carrierUser.majorMarkets);
                ViewData["TrailerType"] = new SelectList(await _trailerService.GetActiveTrailer(), "trailerId", "trailerName", carrierUser.CarrierwiseTrailer);
                ViewData["VehicleTypes"] = new SelectList(await _vehicleTypeService.GetActiveVehicleTypes(), "vehicleId", "vehicleName", carrierUser.CarrierwiseVehicle);
                ViewData["CargoSpecialities"] = new SelectList(await _cargoService.GetActiveCargoSpecialities(), "cargoId", "cargoName", carrierUser.cargoSpecification);
                ViewData["PaymentTypes"] = new SelectList(await _paymentService.GetActivePaymentTypes(), "paymentTypeId", "paymentName", carrierUser.paymentMethods);
                return View(carrierUser);
            }
            catch
            {
                return View();
            }
        }

        // POST: CarrierController/Delete/5
        [HttpPost]
        public async Task<ActionResult> Delete(string id)
        {
            bool isActive = await _carrierProcessingService.IsActive(Convert.ToInt32(HttpContext.Session.GetString("UserID")), "Carrier/Delete" + id);
            if (!isActive)
            {
                return Json("delete");
            }
            carrierusers carrierusers = await _carrierService.GetCarrierusersById(Convert.ToInt32(id));
            carrierusers.isDeleted = true;
            await _carrierService.EditCarrierUser(carrierusers, HttpContext.Session.GetString("UserID"));
            return Json("Carrier deleted successfully");
        }
        [HttpPost]
        public async Task<ActionResult> FillCity(string stateId)
        {
            return Json(await _cityService.GetCitiesById(Convert.ToInt32(stateId)));
        }
        [HttpPost]
        public async Task<ActionResult> ChangeStatus(string id, string status)
        {
            try
            {
                bool isActive = await _carrierProcessingService.IsActive(Convert.ToInt32(HttpContext.Session.GetString("UserID")), "Carrier/ChangeStatus" + id);
                if (!isActive)
                {
                    return Json("null");
                }
                carrierusers carrierusers = await _carrierService.GetCarrierusersById(Convert.ToInt32(id));
                carrierusers.modifiedBy = Convert.ToInt32(HttpContext.Session.GetString("UserID"));
                int tempStatus = Convert.ToInt32(status);
                carrierusers.status = (StatusEnum)tempStatus;
                if (carrierusers.status == StatusEnum.Inprocess)
                {
                    carrierusers.assignee = Convert.ToInt32(HttpContext.Session.GetString("UserID"));
                }
                await _carrierService.EditCarrierUser(carrierusers, HttpContext.Session.GetString("UserID"));
                SmtpClient client = new SmtpClient(MailDetails.SmtpServer, MailDetails.Port);
                client.Credentials = new NetworkCredential(MailDetails.UserName, MailDetails.Password);
                client.EnableSsl = true;
                MailMessage message = new MailMessage();
                message.From = new MailAddress(MailDetails.Sender);
                message.To.Add(new MailAddress(carrierusers.cuEmail));
                message.IsBodyHtml = true; //to make message body as html  
                if (carrierusers.status == StatusEnum.Inprocess)
                {

                    message.Subject = "Onboarding Started";
                    string OnboardingFile = $@"{_hostingEnvironment.WebRootPath}/\MailTemplate\/Onboarding.html";
                    var UserHTML = string.Join("", System.IO.File.ReadAllLines(OnboardingFile));
                    UserHTML = UserHTML.Replace("##Date##", DateTime.Now.ToString("MMM dd, yyyy")).Replace("##Name##", carrierusers.authorizedPerson);
                    message.Body = UserHTML;
                    //await client.SendMailAsync(message);
                }
                else if (carrierusers.status == StatusEnum.Complete)
                {
                    message.Subject = "Onboarding Completed";
                    string OnboardingCompleteFile = $@"{_hostingEnvironment.WebRootPath}/\MailTemplate\/OnboardingComplete.html";
                    var UserHTML = string.Join("", System.IO.File.ReadAllLines(OnboardingCompleteFile));
                    UserHTML = UserHTML.Replace("##Date##", DateTime.Now.ToString("MMM dd, yyyy")).Replace("##Name##", carrierusers.authorizedPerson);
                    message.Body = UserHTML;
                    //await client.SendMailAsync(message);
                }
                else if (carrierusers.status == StatusEnum.Approved)
                {
                    message.Subject = "Registration  Approved";
                    string OnboardingFile = $@"{_hostingEnvironment.WebRootPath}/\MailTemplate\/Approved.html";
                    var UserHTML = string.Join("", System.IO.File.ReadAllLines(OnboardingFile));
                    UserHTML = UserHTML.Replace("##Date##", DateTime.Now.ToString("MMM dd, yyyy")).Replace("##Name##", carrierusers.authorizedPerson);
                    message.Body = UserHTML;
                    //await client.SendMailAsync(message);
                }
                else if (carrierusers.status == StatusEnum.Rejected)
                {
                    message.Subject = "Registration Rejected";
                    string OnboardingFile = $@"{_hostingEnvironment.WebRootPath}/\MailTemplate\/Rejected.html";
                    var UserHTML = string.Join("", System.IO.File.ReadAllLines(OnboardingFile));
                    UserHTML = UserHTML.Replace("##Date##", DateTime.Now.ToString("MMM dd, yyyy")).Replace("##Name##", carrierusers.authorizedPerson);
                    message.Body = UserHTML;
                    //await client.SendMailAsync(message);
                }
                else if (carrierusers.status == StatusEnum.Onhold)
                {
                    message.Subject = "Registration On-hold";
                    string OnboardingFile = $@"{_hostingEnvironment.WebRootPath}/\MailTemplate\/OnHold.html";
                    var UserHTML = string.Join("", System.IO.File.ReadAllLines(OnboardingFile));
                    UserHTML = UserHTML.Replace("##Date##", DateTime.Now.ToString("MMM dd, yyyy")).Replace("##Name##", carrierusers.authorizedPerson);
                    message.Body = UserHTML;
                   // await client.SendMailAsync(message);
                }

                try
                {
                    // to call SendMailAsync method in common and commented in all the conditions above
                    await client.SendMailAsync(message);
                }
                catch (Exception ex)
                {
                    _jWLDBContext.errortracelog.Add(new errortracelog()
                    {
                        error = ex.ToString(),
                        errorControl = "CarrierController/ChangeStatus_SendMailAsync",
                        errorMessage = ex.Message,
                        errorName = ex.Source,
                        errorStack = ex.StackTrace,
                        errorTime = DateTime.Now
                    });
                    await _jWLDBContext.SaveChangesAsync();
                }
                string stringValue = carrierusers.status.ToString();
                return Json("Status changed successfully," + stringValue);
            }
            catch (Exception ex)
            {
                return Json(ex.Message);
            }
        }
        [HttpPost]
        public string UploadSignature()
        {
            string result = string.Empty;
            try
            {
                long size = 0;
                var file = Request.Form.Files;
                var filename = ContentDispositionHeaderValue.Parse(file[0].ContentDisposition).FileName.Trim('"');
                string name = @"/\signature/\sig_" + DateTime.Now.ToString("MM-dd-yyyy") + filename;
                string FilePath = _hostingEnvironment.WebRootPath + name;
                size += file[0].Length;
                using (FileStream fs = System.IO.File.Create(FilePath))
                {
                    file[0].CopyTo(fs);
                    fs.Flush();
                }

                result = this.Request.Scheme + @"://" + this.Request.Host + this.Request.PathBase + name;
            }
            catch (Exception ex)
            {
                result = ex.Message;
            }
            return result;
        }
        [HttpPost]
        public async Task<string> UploadAttachment()
        {
            string result = string.Empty;
            try
            {
                if (HttpContext.Session.GetString("UserID") != null && HttpContext.Session.GetString("UserID") != "")
                {
                    bool isActive = await _carrierProcessingService.IsActive(Convert.ToInt32(HttpContext.Session.GetString("UserID")), "Carrier/RemoveFile");
                    if (!isActive)
                    {
                        return "null";
                    }
                }

                long size = 0;
                IFormFileCollection file = Request.Form.Files;
                string selectedVal = Request.Form["selectedVal"].ToString();
                var filename = ContentDispositionHeaderValue.Parse(file[0].ContentDisposition).FileName.Trim('"');

                string ret = Regex.Replace(filename.Trim(), "[^A-Za-z0-9_. ]+", "");
                ret.Replace(" ", String.Empty);

                string name = $@"/\Document/\att_{DateTime.Now.ToString("MM-dd-yyyy")} {Guid.NewGuid()} {ret}";
                string FilePath = _hostingEnvironment.WebRootPath + name;
                size += file[0].Length;
                using (FileStream fs = System.IO.File.Create(FilePath))
                {
                    file[0].CopyTo(fs);
                    fs.Flush();
                }
                result = this.Request.Scheme + @"://" + this.Request.Host + this.Request.PathBase + name;
            }
            catch (Exception ex)
            {
                result = ex.Message;
            }
            return result;
        }
        [HttpPost]
        public async Task<JsonResult> RemoveFile(string file)
        {
            string result = string.Empty;
            try
            {
                if (HttpContext.Session.GetString("UserID") != null && HttpContext.Session.GetString("UserID") != "")
                {
                    bool isActive = await _carrierProcessingService.IsActive(Convert.ToInt32(HttpContext.Session.GetString("UserID")), "Carrier/RemoveFile");
                    if (!isActive)
                    {
                        return Json("null");
                    }
                }
                string FileName = Path.GetFileName(file);
                System.IO.File.Delete(_hostingEnvironment.WebRootPath + FileName);
                await _carrierService.DeleteAttachment(FileName, HttpContext.Session.GetString("UserID"));
                result = "Success";
            }
            catch (Exception ex)
            {
                result = ex.Message;
            }
            return Json(result);
        }
        [HttpPost]
        public async Task<JsonResult> RemoveVehicle(string vehicleId)
        {
            string result = string.Empty;
            try
            {
                if (HttpContext.Session.GetString("UserID") != null && HttpContext.Session.GetString("UserID") != "")
                {
                    bool isActive = await _carrierProcessingService.IsActive(Convert.ToInt32(HttpContext.Session.GetString("UserID")), "Carrier/RemoveVehicle");
                    if (!isActive)
                    {
                        return Json("null");
                    }
                    await _carrierService.DeleteVehicle(Convert.ToInt32(vehicleId), HttpContext.Session.GetString("UserID"));
                }

                result = "Success";
            }
            catch (Exception ex)
            {
                result = ex.Message;
            }
            return Json(result);
        }
        [HttpPost]
        public async Task<JsonResult> RemoveTrailer(string trailerId)
        {
            string result = string.Empty;
            try
            {
                if (HttpContext.Session.GetString("UserID") != null && HttpContext.Session.GetString("UserID") != "")
                {
                    bool isActive = await _carrierProcessingService.IsActive(Convert.ToInt32(HttpContext.Session.GetString("UserID")), "Carrier/RemoveTrailer");
                    if (!isActive)
                    {
                        return Json("null");
                    }
                    await _carrierService.DeleteTrailer(Convert.ToInt32(trailerId), HttpContext.Session.GetString("UserID"));
                }

                result = "Success";
            }
            catch (Exception ex)
            {
                result = ex.Message;
            }
            return Json(result);
        }
        [HttpPost]
        public async Task<JsonResult> IsHuman(string token)
        {
            var googlerecaptcha = await _googleRecaptchaService.Verification(token);
            if (googlerecaptcha.success && googlerecaptcha.score >= 0.5)
            {
                return Json(true);
            }
            return Json(false);
        }
        [HttpPost]
        public async Task<JsonResult> CheckEmailDOT(string email, string dot, string isEdit, string id = "0")
        {
            return Json(await _carrierService.CheckEmailDOT(email, dot, Convert.ToBoolean(isEdit), Convert.ToInt32(id)));
        }
        [HttpPost]
        public async Task<JsonResult> CheckDOT(string dot, string isEdit, string id = "0")
        {
            if (HttpContext.Session.GetString("UserID") != null && HttpContext.Session.GetString("UserID") != "")
            {
                bool isActive = await _carrierProcessingService.IsActive(Convert.ToInt32(HttpContext.Session.GetString("UserID")), "Carrier/CheckDOT" + id);
                if (!isActive)
                {
                    return Json("null");
                }
            }
            return Json(await _carrierService.CheckDOT(dot, Convert.ToBoolean(isEdit), Convert.ToInt32(id)));
        }
        [HttpPost]
        public async Task<JsonResult> CheckEmail(string email, string isEdit, string id = "0")
        {
            if (HttpContext.Session.GetString("UserID") != null && HttpContext.Session.GetString("UserID") != "")
            {
                bool isActive = await _carrierProcessingService.IsActive(Convert.ToInt32(HttpContext.Session.GetString("UserID")), "Carrier/CheckEmail" + id);
                if (!isActive)
                {
                    return Json("null");
                }
            }
            return Json(await _carrierService.CheckEmail(email, Convert.ToBoolean(isEdit), Convert.ToInt32(id)));
        }
        [HttpPost]
        public async Task SendMail(string Name, string Email)
        {
            try
            {
                SmtpClient client = new SmtpClient(MailDetails.SmtpServer, MailDetails.Port);
                client.Credentials = new NetworkCredential(MailDetails.UserName, MailDetails.Password);
                client.EnableSsl = true;
                MailMessage message = new MailMessage();
                message.From = new MailAddress(MailDetails.Sender);
                message.To.Add(new MailAddress(Email));
                message.Subject = "Registration Request Received";
                message.IsBodyHtml = true; //to make message body as html  
                string RegisterFile = $@"{_hostingEnvironment.WebRootPath}/\MailTemplate\/Registration.html";
                var RegisterMailHTML = string.Join("", System.IO.File.ReadAllLines(RegisterFile));
                RegisterMailHTML = RegisterMailHTML.Replace("##Date##", DateTime.Now.ToString("MMM dd, yyyy")).Replace("##Person##", Name);
                message.Body = RegisterMailHTML;
                await client.SendMailAsync(message);
            }
            catch (Exception ex)
            {
                _jWLDBContext.errortracelog.Add(new errortracelog()
                {
                    error = ex.ToString(),
                    errorControl = "CarrierController/SendMail",
                    errorMessage = ex.Message,
                    errorName = ex.Source,
                    errorStack = ex.StackTrace,
                    errorTime = DateTime.Now
                });
                await _jWLDBContext.SaveChangesAsync();
            }
        }
        [HttpGet]
        //Carrier/ExportToExcel
        public async Task<IActionResult> ExportToExcel()
        {
            try
            {
                bool isActive = await _carrierProcessingService.IsActive(Convert.ToInt32(HttpContext.Session.GetString("UserID")), "Carrier/ExportToExcel");
                if (!isActive)
                {
                    return RedirectToAction("Login", "Users");
                }
                List<carrierusers> carrier = await _carrierService.ExportCarrierUsers();
                List<state> marketstates = await _stateService.GetStates();
                List<paymenttype> paymenttypes = await _paymentService.GetPaymentTypes();
                List<cargospecialties> cargo = await _cargoService.GetCargoSpecialities();
                List<CarrierViewModel> carrierViewModels = new List<CarrierViewModel>();
                foreach (var item in carrier)
                {
                    List<string> SelectedMarket = item.additionalMajorMakets != null ? item.additionalMajorMakets.Split(',').ToList() : new List<string>();
                    List<string> GivenMarket = marketstates.Where(t => SelectedMarket.Contains(t.stateId.ToString())).Select(t => t.stateName).ToList();
                    List<string> SelectedPayment = item.paymentMethods != null ? item.paymentMethods.Split(',').ToList() : new List<string>();
                    List<string> GivenPayment = paymenttypes.Where(t => SelectedPayment.Contains(t.paymentTypeId.ToString())).Select(t => t.paymentName).ToList();
                    List<string> SelectedCargo = item.cargoSpecification != null ? item.cargoSpecification.Split(',').ToList() : new List<string>();
                    List<string> GivenMethod = cargo.Where(t => SelectedCargo.Contains(t.cargoId.ToString())).Select(t => t.cargoName).ToList();
                    string Identification = string.Empty;
                    if (item.ethnicIdentification == "1")
                        Identification = "American Indian or Alaska Native";
                    else if (item.ethnicIdentification == "2")
                        Identification = "Asian";
                    else if (item.ethnicIdentification == "3")
                        Identification = "Black or African American";
                    else if (item.ethnicIdentification == "4")
                        Identification = "Hispanic or Latino";
                    else if (item.ethnicIdentification == "5")
                        Identification = "Native Hawaiian or Other Pacific Islander";
                    else if (item.ethnicIdentification == "6")
                        Identification = "White";
                    else if (item.ethnicIdentification == "7")
                        Identification = "Other";
                    else if (item.ethnicIdentification == "8")
                        Identification = "I prefer not to say";

                    List<vehicles> vehicles = JsonConvert.DeserializeObject<List<vehicles>>(item.CarrierwiseVehicle);

                    List<tempTrailer> trailers = JsonConvert.DeserializeObject<List<tempTrailer>>(item.CarrierwiseTrailer);
                    List<authorizedpath> authorizedpaths = JsonConvert.DeserializeObject<List<authorizedpath>>(item.CarrierDocument);

                    carrierViewModels.Add(new CarrierViewModel()
                    {
                        cuId = item.cuId.ToString(),
                        cuEmail = item.cuEmail != null ? item.cuEmail : string.Empty,
                        agreementDate = item.agreementDate != null ? item.agreementDate.ToString("MM/dd/yyyy") : string.Empty,
                        MC = item.MC != null ? item.MC : string.Empty,
                        authorizedPerson = item.authorizedPerson != null ? item.authorizedPerson : string.Empty,
                        title = item.title != null ? item.title : string.Empty,
                        legalCompanyName = item.legalCompanyName != null ? item.legalCompanyName : string.Empty,
                        DBA = item.DBA != null ? item.DBA : string.Empty,
                        physicalAddress = item.physicalAddress != null ? item.physicalAddress : string.Empty,
                        city = item.city != null ? item.city : string.Empty,
                        state = item.States.stateName,
                        zipcode = item.zipcode != null ? item.zipcode : string.Empty,
                        telephone = item.telephone != null ? item.telephone : string.Empty,
                        fax = item.fax != null ? item.fax : string.Empty,
                        status = item.status.ToString(),
                        factoryCompanyName = item.factoryCompanyName != null ? item.factoryCompanyName : string.Empty,
                        factoryContactName = item.factoryContactName != null ? item.factoryContactName : string.Empty,
                        factoryPhysicalAddress = item.factoryPhysicalAddress != null ? item.factoryPhysicalAddress : string.Empty,
                        factoryCity = item.factoryCity != null ? item.factoryCity : string.Empty,
                        factoryState = item.FactoryStates != null && item.FactoryStates.stateName != null ? item.FactoryStates.stateName : string.Empty,
                        factoryZipCode = item.factoryZipCode != null ? item.factoryZipCode : string.Empty,
                        factoryTelephone = item.factoryTelephone != null ? item.factoryTelephone : string.Empty,
                        factoryFax = item.factoryFax != null ? item.factoryFax : string.Empty,
                        additionalPersonName = item.additionalPersonName != null ? item.additionalPersonName : string.Empty,
                        addtionalPersonTelephone = item.addtionalPersonTelephone != null ? item.addtionalPersonTelephone : string.Empty,
                        addtionalAfterHoursPersonName = item.addtionalAfterHoursPersonName != null ? item.addtionalAfterHoursPersonName : string.Empty,
                        addtionalAfterHoursPersonTelephone = item.addtionalAfterHoursPersonTelephone != null ? item.addtionalAfterHoursPersonTelephone : string.Empty,
                        addtionalDot = item.addtionalDot != null ? item.addtionalDot : string.Empty,
                        additionalScac = item.additionalScac != null ? item.additionalScac : string.Empty,
                        additionalFedaralID = item.additionalFedaralID != null ? item.additionalFedaralID : string.Empty,
                        additionalHazmatCertified = item.additionalHazmatCertified == null ? string.Empty : item.additionalHazmatCertified == false ? "No" : "Yes",
                        additinalHazmatExpiryDate = item.additinalHazmatExpiryDate != null ? item.additinalHazmatExpiryDate.Value.ToString("MM/dd/yyyy") : string.Empty,
                        additionalPreferredLanes = item.additionalPreferredLanes,
                        additionalMajorMakets = string.Join(",", GivenMarket),
                        authorizedSignature = item.authorizedSignature != null ? item.authorizedSignature : string.Empty,
                        completedMBCA = item.completedMBCA == null ? string.Empty : item.completedMBCA == false ? "No" : "Yes",
                        reportSortKey = item.reportSortKey != null ? item.reportSortKey : string.Empty,
                        ndaReturned = item.ndaReturned == null ? string.Empty : item.ndaReturned == false ? "No" : "Yes",
                        onboardingCompleted = item.onboardingCompleted == null ? string.Empty : item.onboardingCompleted == false ? "No" : "Yes",
                        Ins1MGeneral = item.Ins1MGeneral == null ? string.Empty : item.Ins1MGeneral == false ? "No" : "Yes",
                        Ins1MAuto = item.Ins1MAuto == null ? string.Empty : item.Ins1MAuto == false ? "No" : "Yes",
                        Ins100KCargo = item.Ins100KCargo == null ? string.Empty : item.Ins100KCargo == false ? "No" : "Yes",
                        Ins250KCargo = item.Ins250KCargo == null ? string.Empty : item.Ins250KCargo == false ? "No" : "Yes",
                        paymentTerms = item.paymentTerms != null ? item.paymentTerms : string.Empty,
                        CreatedDate = item.CreatedDate != null ? item.CreatedDate.ToString("MM/dd/yyyy") : string.Empty,
                        createdBy = item.cuId == item.createdBy ? item.authorizedPerson : item.CreatedUserName,//item.createdByUserName
                        ModifiedDate = item.modifiedBy != null ? item.ModifiedDate.ToString("MM/dd/yyy") : string.Empty,
                        twic = item.twic == null ? string.Empty : item.twic == false ? "No" : "Yes",
                        ca = item.ca != null ? item.ca : string.Empty,
                        isFemaleOwned = item.isFemaleOwned == null ? string.Empty : item.isFemaleOwned == false ? "No" : "Yes",
                        isVeteranOwned = item.isVeteranOwned == null ? string.Empty : item.isVeteranOwned == false ? "No" : "Yes",
                        modifiedBy = item.modifiedByUser != null && item.modifiedByUser.name != null ? item.modifiedByUser.name : string.Empty,
                        assignee = item.Users != null && item.Users.name != null ? item.Users.name : string.Empty,
                        isMinorityBusiness = item.isMinorityBusiness == null ? string.Empty : item.isMinorityBusiness == false ? "No" : "Yes",
                        businessYear = item.businessYear != null ? item.businessYear : string.Empty,
                        milesPerYear = item.milesPerYear != null ? item.milesPerYear : string.Empty,
                        cargoSpecification = string.Join(",", GivenMethod),
                        paymentMethods = string.Join(",", GivenPayment),
                        serviceArea = item.serviceArea != null ? item.serviceArea : string.Empty,
                        brokerOptions = item.brokerOptions != null ? item.brokerOptions : string.Empty,
                        ethnicIdentification = Identification,
                        ExportVehicle = vehicles != null && vehicles.Count > 0 ? vehicles[0].vehicltype.vehicleName : string.Empty, //VehicleType != null && VehicleType != "" ? VehicleType.Remove(VehicleType.Length - 1, 1) : string.Empty,
                        FleetVehicle = vehicles != null && vehicles.Count > 0 ? vehicles[0].numberOfVehicle : string.Empty,
                        ExportTrailer = trailers != null && trailers.Count > 0 ? trailers[0].trailer.trailerName : string.Empty,//TrailerType != null && TrailerType != "" ? TrailerType.Remove(TrailerType.Length - 1, 1) : string.Empty,
                        ExportTrailerCount = trailers != null && trailers.Count > 0 ? trailers[0].numberOfVehicle : string.Empty,
                        CarrierDocumentUrl = authorizedpaths != null && authorizedpaths.Count > 0 ? authorizedpaths[0].documentPath.Replace(@"/\", @"//") : string.Empty,
                        CarrierDocument = authorizedpaths != null && authorizedpaths.Count > 0 ? authorizedpaths[0].selectedOptions.Replace("_txt", "Other-") : string.Empty
                    });

                    int max = 0; //vehicles.Count > trailers.Count ? vehicles.Count : trailers.Count;

                    if (vehicles != null && trailers != null && vehicles.Count > trailers.Count)
                    {
                        if (authorizedpaths != null)
                        {
                            if (vehicles.Count > authorizedpaths.Count)
                            {
                                max = vehicles.Count;
                            }
                            else
                            {
                                max = authorizedpaths.Count;
                            }
                        }
                        else
                        {
                            max = vehicles.Count;
                        }
                    }
                    else if (vehicles != null && trailers != null && trailers.Count > authorizedpaths.Count)
                    {
                        if (authorizedpaths != null)
                        {
                            if (trailers.Count > authorizedpaths.Count)
                            {
                                max = trailers.Count;
                            }
                            else
                            {
                                max = authorizedpaths.Count;
                            }
                        }
                        else
                        {
                            max = trailers.Count;
                        }
                        max = trailers.Count;
                    }
                    else if (vehicles != null && trailers == null && authorizedpaths == null && vehicles.Count > 0)
                    {
                        max = vehicles.Count;
                    }
                    else if (trailers != null && vehicles == null && authorizedpaths == null && trailers.Count > 0)
                    {
                        max = trailers.Count;
                    }
                    else
                    {
                        max = authorizedpaths.Count;
                    }
                    for (int i = 1; i < max; i++)
                    {
                        bool isTrailer = i < trailers.Count;
                        bool isVehicle = i < vehicles.Count;
                        bool isPath = i < authorizedpaths.Count;
                        if (isTrailer || isVehicle || isPath)
                        {
                            carrierViewModels.Add(new CarrierViewModel()
                            {
                                ExportVehicle = isVehicle && vehicles[i] != null ? vehicles[i].vehicltype.vehicleName : string.Empty,
                                FleetVehicle = isVehicle && vehicles[i] != null ? vehicles[i].numberOfVehicle : string.Empty,
                                ExportTrailer = isTrailer && trailers[i] != null ? trailers[i].trailer.trailerName : string.Empty,
                                ExportTrailerCount = isTrailer && trailers[i] != null ? trailers[i].numberOfVehicle : string.Empty,
                                CarrierDocumentUrl = isPath && authorizedpaths[i] != null ? authorizedpaths[i].documentPath.Replace(@"/\", @"//") : string.Empty,
                                CarrierDocument = isPath && authorizedpaths[i] != null ? authorizedpaths[i].selectedOptions.Replace("_txt", "Other-") : string.Empty,
                            });
                        }
                    }
                }
                MemoryStream memory = new MemoryStream();
                using (ExcelPackage package = new ExcelPackage(memory))
                {
                    ExcelWorksheet worksheet = package.Workbook.Worksheets.Add("Carrier");
                    worksheet.Cells.LoadFromCollection(carrierViewModels, true);
                    worksheet.Cells[worksheet.Dimension.Address].AutoFitColumns();
                    package.Save();

                }
                memory.Position = 0;
                string FileName = $"Carrier{DateTime.Now.ToString("MMddyyyy hh:mm tt")}.xlsx";
                return File(memory, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", FileName);
            }


            catch (Exception ex)
            {
                return RedirectToAction("managecarriers");

            }
        }
    }

}

