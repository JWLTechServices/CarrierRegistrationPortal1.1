using Interfaces;
using Microsoft.EntityFrameworkCore;
using Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;

namespace Data
{
    public class CarrierService : ICarrierService
    {
        private readonly JWLDBContext _jWLDBContext;
        private readonly IVehicleTypeService _vehicleTypeService;
        public CarrierService(JWLDBContext jWLDBContext, IVehicleTypeService vehicleTypeService)
        {
            _jWLDBContext = jWLDBContext;
            _vehicleTypeService = vehicleTypeService;
        }
        public async Task<List<carrierusers>> ExportCarrierUsers()
        {
            List<carrierusers> users = await _jWLDBContext.carrierusers.AsNoTracking().Where(t => t.isDeleted == null || t.isDeleted == false).Include(t => t.States).Include(t => t.FactoryStates).Include(t => t.Users).Include(t => t.modifiedByUser).ToListAsync();
            for (int Userscounter = 0; Userscounter < users.Count; Userscounter++)
            {
                List<carriervehicle> carriervehicles = await _jWLDBContext.carriervehicle.AsNoTracking().Where(t => t.carrierId == users[Userscounter].cuId && (t.isDeleted == null || t.isDeleted.Value == false)).Include(t => t.vehicltype).ToListAsync();
                List<carrierTrailer> carrierTrailer = await _jWLDBContext.carrierTrailer.AsNoTracking().Where(t => t.carrierId == users[Userscounter].cuId && (t.isDeleted == null || t.isDeleted.Value == false)).Include(t => t.trailer).ToListAsync();
                List<authorizedpath> authorizedpath = await _jWLDBContext.authorizedpath.AsNoTracking().Where(t => t.carrierId == users[Userscounter].cuId && (t.isDeleted == null || t.isDeleted.Value == false)).ToListAsync();
                List<vehicles> vehicles = new List<vehicles>();
                for (int i = 0; i < carriervehicles.Count; i++)
                {
                    vehicles.Add(new vehicles()
                    {
                        id = carriervehicles[i].carrierVehicleId.ToString(),
                        numberOfVehicle = carriervehicles[i].numberOfVehicle.ToString(),
                        selectedVehicle = carriervehicles[i].vehicleId.ToString(),
                        vehicltype = carriervehicles[i].vehicltype
                    });
                }
                List<tempTrailer> tempTrailer = new List<tempTrailer>();
                for (int i = 0; i < carrierTrailer.Count; i++)
                {
                    tempTrailer.Add(new tempTrailer()
                    {
                        id = carrierTrailer[i].carrierTrailerId.ToString(),
                        numberOfVehicle = carrierTrailer[i].numberOfVehicle.ToString(),
                        selectedTrailer = carrierTrailer[i].trailerId.ToString(),
                        trailer = carrierTrailer[i].trailer
                    });
                }
                users[Userscounter].CarrierDocument = JsonConvert.SerializeObject(authorizedpath);
                users[Userscounter].CarrierwiseVehicle = JsonConvert.SerializeObject(vehicles);
                users[Userscounter].CarrierwiseTrailer = JsonConvert.SerializeObject(tempTrailer);
            }
            users = users.OrderBy(t => t.cuId).ToList();
            return users;

        }
        public async Task DeleteAttachment(string File, string UserID)
        {
            var a = await _jWLDBContext.authorizedpath.Where(t => t.documentPath.Contains(File)).FirstOrDefaultAsync();
            if (a != null && a.authorizedId != null && a.authorizedId != 0)
            {
                a.isDeleted = true;
                if (UserID != null && UserID != "" && UserID != "0")
                {
                    await _jWLDBContext.SaveChanges(UserID);
                }
                else
                {
                    await _jWLDBContext.SaveChangesAsync();
                }
            }
        }
        public async Task DeleteTrailer(int TrailerID, string UserID)
        {
            var a = await _jWLDBContext.carrierTrailer.Where(t => t.carrierTrailerId == TrailerID).FirstOrDefaultAsync();
            if (a != null && a.carrierTrailerId != null && a.carrierTrailerId != 0)
            {
                a.isDeleted = true;
                if (UserID != null && UserID != "" && UserID != "0")
                {
                    await _jWLDBContext.SaveChanges(UserID);
                }
                else
                {
                    await _jWLDBContext.SaveChangesAsync();
                }
            }
        }
        public async Task DeleteVehicle(int VehicleID, string UserID)
        {
            var a = await _jWLDBContext.carriervehicle.Where(t => t.carrierVehicleId == VehicleID).FirstOrDefaultAsync();
            if (a != null && a.carrierVehicleId != null && a.carrierVehicleId != 0)
            {
                a.isDeleted = true;
                if (UserID != null && UserID != "" && UserID != "0")
                {
                    await _jWLDBContext.SaveChanges(UserID);
                }
                else
                {
                    await _jWLDBContext.SaveChangesAsync();
                }
            }

        }
        public async Task<List<carrierusers>> GetCarrierUsers()
        {
            try
            {
                //createdby and cuId same hoy to authorized name else 
                List<carrierusers> users = await _jWLDBContext.carrierusers.AsNoTracking().Where(t => t.isDeleted == null || t.isDeleted == false).Include(t => t.States).Include(t => t.FactoryStates).Include(t => t.Users).Include(t => t.modifiedByUser).ToListAsync();
                for (int i = 0; i < users.Count; i++)
                {
                    if (users[i].cuId != users[i].createdBy)
                    {
                        users[i].CreatedUserName = await _jWLDBContext.users.AsNoTracking().Where(t => t.userId == users[i].createdBy).Select(t => t.name).FirstOrDefaultAsync();
                    }
                }
                users = users.OrderByDescending(t => t.CreatedDate).ToList();
                return users;

            }
            catch (Exception ex)
            {
                _jWLDBContext.errortracelog.Add(new errortracelog()
                {
                    error = ex.ToString(),
                    errorControl = "CarrierService/GetCarrierUsers",
                    errorMessage = ex.Message,
                    errorName = ex.Source,
                    errorStack = ex.StackTrace,
                    errorTime = DateTime.Now
                });
                await _jWLDBContext.SaveChangesAsync();
                return new List<carrierusers>();
            }
        }

        public async Task<bool> IsAlreadyExists(carrierusers carrierusers, bool IsInEdit = false)
        {
            try
            {
                if (!IsInEdit)
                {
                    return await _jWLDBContext.carrierusers.AnyAsync(t => t.cuEmail.ToLower() == carrierusers.cuEmail.ToLower());
                }
                else
                {
                    return await _jWLDBContext.carrierusers.AnyAsync(t => t.cuEmail.ToLower() == carrierusers.cuEmail.ToLower() && t.cuId != carrierusers.cuId);
                }
            }
            catch (Exception ex)
            {

                _jWLDBContext.errortracelog.Add(new errortracelog()
                {
                    error = ex.ToString(),
                    errorControl = "CarrierService/IsAlreadyExists",
                    errorMessage = ex.Message,
                    errorName = ex.Source,
                    errorStack = ex.StackTrace,
                    errorTime = DateTime.Now
                });
                await _jWLDBContext.SaveChangesAsync();
                return false;
            }


        }
        public async Task AddCarrierUser(carrierusers carrierUser, string UserID)
        {
            try
            {
                UserID = UserID == null ? "0" : UserID;
                carrierUser.CreatedDate = carrierUser.ModifiedDate = DateTime.Now;
                var Added = await _jWLDBContext.carrierusers.AddAsync(carrierUser);
                await _jWLDBContext.SaveChanges(UserID);
                if (carrierUser.assignee == null)
                {
                    carrierUser.modifiedBy = carrierUser.createdBy = carrierUser.cuId;
                    if (UserID == null || UserID == "0")
                    {
                        _jWLDBContext.carrierusers.Update(carrierUser);
                        await _jWLDBContext.SaveChangesAsync();
                    }
                    else
                    {
                        await _jWLDBContext.SaveChanges(UserID);
                    }
                }

                if (carrierUser.CarrierwiseVehicle != null && carrierUser.CarrierwiseVehicle != "")
                {
                    List<vehicles> vehicles = JsonConvert.DeserializeObject<List<vehicles>>(carrierUser.CarrierwiseVehicle);

                    for (int i = 0; i < vehicles.Count; i++)
                    {
                        carriervehicle vehicltype = new carriervehicle()
                        {
                            carrierId = carrierUser.cuId,
                            numberOfVehicle = Convert.ToInt32(vehicles[i].numberOfVehicle),
                            vehicleId = Convert.ToInt32(vehicles[i].selectedVehicle)
                        };
                        await _jWLDBContext.carriervehicle.AddAsync(vehicltype);
                        if (UserID == null || UserID == "0")
                        {
                            await _jWLDBContext.SaveChangesAsync();
                        }
                        else
                        {
                            await _jWLDBContext.SaveChanges(UserID);
                        }
                    }
                }
                if (carrierUser.CarrierwiseTrailer != null && carrierUser.CarrierwiseTrailer != "")
                {
                    List<tempTrailer> vehicles = JsonConvert.DeserializeObject<List<tempTrailer>>(carrierUser.CarrierwiseTrailer);

                    for (int i = 0; i < vehicles.Count; i++)
                    {
                        carrierTrailer vehicltype = new carrierTrailer()
                        {
                            carrierId = carrierUser.cuId,
                            numberOfVehicle = Convert.ToInt32(vehicles[i].numberOfVehicle),
                            trailerId = Convert.ToInt32(vehicles[i].selectedTrailer)
                        };
                        await _jWLDBContext.carrierTrailer.AddAsync(vehicltype);
                        if (UserID == null || UserID == "0")
                        {
                            await _jWLDBContext.SaveChangesAsync();
                        }
                        else
                        {
                            await _jWLDBContext.SaveChanges(UserID);
                        }
                    }

                }
                if (carrierUser.CarrierDocument != null && carrierUser.CarrierDocument != "")
                {
                    List<attachmentPath> paths = JsonConvert.DeserializeObject<List<attachmentPath>>(carrierUser.CarrierDocument);
                    for (int i = 0; i < paths.Count; i++)
                    {
                        authorizedpath authorizedpath = new authorizedpath()
                        {
                            carrierId = carrierUser.cuId,
                            documentPath = paths[i].url,
                            selectedOptions = paths[i].selectedVal
                        };
                        await _jWLDBContext.authorizedpath.AddAsync(authorizedpath);
                        if (UserID == null || UserID == "0")
                        {
                            await _jWLDBContext.SaveChangesAsync();
                        }
                        else
                        {
                            await _jWLDBContext.SaveChanges(UserID);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _jWLDBContext.errortracelog.Add(new errortracelog()
                {
                    error = ex.ToString(),
                    errorControl = "CarrierService/AddCarrierUser",
                    errorMessage = ex.Message,
                    errorName = ex.Source,
                    errorStack = ex.StackTrace,
                    errorTime = DateTime.Now
                });
                await _jWLDBContext.SaveChangesAsync();
            }

        }
        public async Task<string> CheckEmailDOT(string Email, string DOT, bool IsEdit, int id = 0)
        {
            try
            {
                var Carriers = await _jWLDBContext.carrierusers.AsNoTracking().ToListAsync();

                if (!IsEdit)
                {
                    bool IsEmail = Carriers.Any(t => t.cuEmail.ToLower() == Email.ToLower());
                    bool IsDOT = Carriers.Any(t => t.addtionalDot.ToLower() == DOT.ToLower());
                    return IsEmail + "," + IsDOT;
                }
                else
                {
                    bool IsEmail = Carriers.Any(t => t.cuEmail.ToLower() == Email.ToLower() && t.cuId != id);
                    bool IsDOT = Carriers.Any(t => t.addtionalDot.ToLower() == DOT.ToLower() && t.cuId != id);
                    return IsEmail + "," + IsDOT;
                }
            }
            catch (Exception ex)
            {

                _jWLDBContext.errortracelog.Add(new errortracelog()
                {
                    error = ex.ToString(),
                    errorControl = "CarrierService/CheckEmailDOT",
                    errorMessage = ex.Message,
                    errorName = ex.Source,
                    errorStack = ex.StackTrace,
                    errorTime = DateTime.Now
                });
                await _jWLDBContext.SaveChangesAsync();
                return false.ToString();
            }

        }
        public async Task<string> CheckDOT(string DOT, bool IsEdit, int id = 0)
        {
            try
            {
                var Carriers = await _jWLDBContext.carrierusers.AsNoTracking().ToListAsync();
                if (!IsEdit)
                {
                    bool IsDOT = Carriers.Any(t => t.addtionalDot.ToLower() == DOT.ToLower());
                    return IsDOT.ToString();
                }
                else
                {
                    bool IsDOT = Carriers.Any(t => t.addtionalDot.ToLower() == DOT.ToLower() && t.cuId != id);
                    return IsDOT.ToString();
                }
            }
            catch (Exception ex)
            {

                _jWLDBContext.errortracelog.Add(new errortracelog()
                {
                    error = ex.ToString(),
                    errorControl = "CarrierService/CheckDOT",
                    errorMessage = ex.Message,
                    errorName = ex.Source,
                    errorStack = ex.StackTrace,
                    errorTime = DateTime.Now
                });
                await _jWLDBContext.SaveChangesAsync();
                return false.ToString();
            }

        }

        public async Task<string> CheckEmail(string Email, bool IsEdit, int id = 0)
        {
            try
            {
                var Carriers = await _jWLDBContext.carrierusers.AsNoTracking().ToListAsync();
                if (!IsEdit)
                {
                    bool IsEmail = Carriers.Any(t => t.cuEmail.ToLower() == Email.ToLower());
                    return IsEmail.ToString();
                }
                else
                {
                    bool IsEmail = Carriers.Any(t => t.cuEmail.ToLower() == Email.ToLower() && t.cuId != id);
                    return IsEmail.ToString();
                }
            }
            catch (Exception ex)
            {
                _jWLDBContext.errortracelog.Add(new errortracelog()
                {
                    error = ex.ToString(),
                    errorControl = "CarrierService/CheckEmail",
                    errorMessage = ex.Message,
                    errorName = ex.Source,
                    errorStack = ex.StackTrace,
                    errorTime = DateTime.Now
                });
                await _jWLDBContext.SaveChangesAsync();
                return false.ToString();
            }

        }
        public async Task UpdateURL(carrierusers carrierUser, string UserID)
        {
            carrierusers carrierusers = await _jWLDBContext.carrierusers.Where(t => t.cuId == carrierUser.cuId).FirstOrDefaultAsync();
            carrierusers.cuName = carrierUser.cuName;
            carrierusers.cuEmail = carrierUser.cuEmail;
            carrierusers.agreementDate = carrierUser.agreementDate;
            carrierusers.agreementDate = carrierUser.agreementDate;
            carrierusers.MC = carrierUser.MC;
            carrierusers.MC = carrierUser.MC;
            carrierusers.authorizedPerson = carrierUser.authorizedPerson;
            carrierusers.title = carrierUser.title;
            carrierusers.legalCompanyName = carrierUser.legalCompanyName;
            carrierusers.DBA = carrierUser.DBA;
            carrierusers.physicalAddress = carrierUser.physicalAddress;
            carrierusers.city = carrierUser.city;
            carrierusers.state = carrierUser.state;
            carrierusers.zipcode = carrierUser.zipcode;
            carrierusers.telephone = carrierUser.telephone;
            carrierusers.fax = carrierUser.fax;
            carrierusers.status = carrierUser.status;
            carrierusers.factoryCompanyName = carrierUser.factoryCompanyName;
            carrierusers.factoryContactName = carrierUser.factoryContactName;
            carrierusers.factoryPhysicalAddress = carrierUser.factoryPhysicalAddress;
            carrierusers.factoryCity = carrierUser.factoryCity;
            carrierusers.factoryState = carrierUser.factoryState;
            carrierusers.factoryZipCode = carrierUser.factoryZipCode;
            carrierusers.factoryTelephone = carrierUser.factoryTelephone;
            carrierusers.factoryFax = carrierUser.factoryFax;
            carrierusers.additionalPersonName = carrierUser.additionalPersonName;
            carrierusers.addtionalPersonTelephone = carrierUser.addtionalPersonTelephone;
            carrierusers.addtionalAfterHoursPersonName = carrierUser.addtionalAfterHoursPersonName;
            carrierusers.addtionalAfterHoursPersonTelephone = carrierUser.addtionalAfterHoursPersonTelephone;
            carrierusers.addtionalDot = carrierUser.addtionalDot;
            carrierusers.additionalScac = carrierUser.additionalScac;
            carrierusers.additionalFedaralID = carrierUser.additionalFedaralID;
            carrierusers.additionalHazmatCertified = carrierUser.additionalHazmatCertified;
            carrierusers.additinalHazmatExpiryDate = carrierUser.additinalHazmatExpiryDate;
            carrierusers.additionalPreferredLanes = carrierUser.additionalPreferredLanes;
            carrierusers.additionalMajorMakets = carrierUser.additionalMajorMakets;
            carrierusers.authorizedSignature = carrierUser.authorizedSignature;
            carrierusers.authorizedSignaturePath = carrierUser.authorizedSignaturePath;
            carrierusers.completedMBCA = carrierUser.completedMBCA;
            carrierusers.reportSortKey = carrierUser.reportSortKey;
            carrierusers.ndaReturned = carrierUser.ndaReturned;
            carrierusers.onboardingCompleted = carrierUser.onboardingCompleted;
            carrierusers.onboardingFileLink = carrierUser.onboardingFileLink;
            carrierusers.insuranceType = carrierUser.insuranceType;
            carrierusers.paymentTerms = carrierUser.paymentTerms;
            carrierusers.majorMarkets = carrierUser.majorMarkets;
            carrierusers.CreatedDate = carrierUser.CreatedDate;
            carrierusers.createdBy = carrierUser.createdBy;
            carrierusers.ModifiedDate = carrierUser.ModifiedDate;
            carrierusers.twic = carrierUser.twic;
            carrierusers.ca = carrierUser.ca;
            carrierusers.isFemaleOwned = carrierUser.isFemaleOwned;
            carrierusers.isVeteranOwned = carrierUser.isVeteranOwned;
            carrierusers.modifiedBy = carrierUser.modifiedBy;
            carrierusers.assignee = carrierUser.assignee;
            carrierusers.isMinorityBusiness = carrierUser.isMinorityBusiness;
            carrierusers.isDeleted = carrierUser.isDeleted;
            carrierusers.businessYear = carrierUser.businessYear;
            carrierusers.ethnicIdentification = carrierUser.ethnicIdentification;
            carrierusers.milesPerYear = carrierUser.milesPerYear;
            carrierusers.paymentMethods = carrierUser.paymentMethods;
            carrierusers.CarrierwiseVehicle = carrierUser.CarrierwiseVehicle;
            carrierusers.CarrierwiseTrailer = carrierUser.CarrierwiseTrailer;
            carrierusers.CarrierDocument = carrierUser.CarrierDocument;
            carrierusers.CreatedUserName = carrierUser.CreatedUserName;
            carrierusers.serviceArea = carrierUser.serviceArea;
            carrierusers.brokerOptions = carrierUser.brokerOptions;
            carrierusers.createdByUserName = carrierUser.createdByUserName;
            carrierusers.cargoSpecification = carrierUser.cargoSpecification;
            carrierusers.Ins1MGeneral = carrierUser.Ins1MGeneral;
            carrierusers.Ins1MAuto = carrierUser.Ins1MAuto;
            carrierusers.Ins250KCargo = carrierUser.Ins250KCargo;
            carrierusers.Ins100KCargo = carrierUser.Ins100KCargo;
            //_jWLDBContext.carrierusers.Update(carrierusers);
            if (UserID == null || UserID == "" || UserID == "0")
            {
                _jWLDBContext.carrierusers.Update(carrierusers);
                await _jWLDBContext.SaveChangesAsync();
            }
            else
            {
                await _jWLDBContext.SaveChanges(UserID);
            }
        }
        public async Task<carrierusers> GetCarrierusersById(int id)
        {
            try
            {
                carrierusers carrierusers = await _jWLDBContext.carrierusers.AsNoTracking().Where(t => t.cuId == id).FirstOrDefaultAsync();
                List<carriervehicle> carriervehicles = await _jWLDBContext.carriervehicle.AsNoTracking().Where(t => t.carrierId == carrierusers.cuId && (t.isDeleted == null || t.isDeleted.Value == false)).ToListAsync();
                List<carrierTrailer> carrierTrailer = await _jWLDBContext.carrierTrailer.AsNoTracking().Where(t => t.carrierId == carrierusers.cuId && (t.isDeleted == null || t.isDeleted.Value == false)).ToListAsync();
                List<vehicles> vehicles = new List<vehicles>();
                for (int i = 0; i < carriervehicles.Count; i++)
                {
                    vehicles.Add(new vehicles()
                    {
                        id = carriervehicles[i].carrierVehicleId.ToString(),
                        numberOfVehicle = carriervehicles[i].numberOfVehicle.ToString(),
                        selectedVehicle = carriervehicles[i].vehicleId.ToString()
                    });
                }
                List<tempTrailer> tempTrailer = new List<tempTrailer>();
                for (int i = 0; i < carrierTrailer.Count; i++)
                {
                    tempTrailer.Add(new tempTrailer()
                    {
                        id = carrierTrailer[i].carrierTrailerId.ToString(),
                        numberOfVehicle = carrierTrailer[i].numberOfVehicle.ToString(),
                        selectedTrailer = carrierTrailer[i].trailerId.ToString()
                    });
                }
                List<authorizedpath> authorizedpath = await _jWLDBContext.authorizedpath.AsNoTracking().Where(t => t.carrierId == carrierusers.cuId && (t.isDeleted == null || t.isDeleted.Value == false)).ToListAsync();
                List<attachmentPath> paths = new List<attachmentPath>();
                for (int i = 0; i < authorizedpath.Count; i++)
                {
                    paths.Add(new attachmentPath()
                    {
                        id = authorizedpath[i].authorizedId.ToString(),
                        selectedVal = authorizedpath[i].selectedOptions,
                        url = authorizedpath[i].documentPath
                    });
                }
                carrierusers.CarrierwiseVehicle = JsonConvert.SerializeObject(vehicles);
                carrierusers.CarrierwiseTrailer = JsonConvert.SerializeObject(tempTrailer);
                carrierusers.CarrierDocument = JsonConvert.SerializeObject(paths);
                return carrierusers;
            }
            catch (Exception ex)
            {

                _jWLDBContext.errortracelog.Add(new errortracelog()
                {
                    error = ex.ToString(),
                    errorControl = "CarrierService/GetCarrierusersById",
                    errorMessage = ex.Message,
                    errorName = ex.Source,
                    errorStack = ex.StackTrace,
                    errorTime = DateTime.Now
                });
                await _jWLDBContext.SaveChangesAsync();
                return null;
            }


        }
        public async Task EditCarrierUser(carrierusers carrierUser, string UserID)
        {
            try
            {
                carrierusers carrierusers = await _jWLDBContext.carrierusers.Where(t => t.cuId == carrierUser.cuId).FirstOrDefaultAsync();
                carrierusers.cuName = carrierUser.cuName;
                carrierusers.cuEmail = carrierUser.cuEmail;
                carrierusers.agreementDate = carrierUser.agreementDate;
                carrierusers.agreementDate = carrierUser.agreementDate;
                carrierusers.MC = carrierUser.MC;
                carrierusers.twic = carrierUser.twic;
                carrierusers.authorizedPerson = carrierUser.authorizedPerson;
                carrierusers.title = carrierUser.title;
                carrierusers.legalCompanyName = carrierUser.legalCompanyName;
                carrierusers.DBA = carrierUser.DBA;
                carrierusers.physicalAddress = carrierUser.physicalAddress;
                carrierusers.city = carrierUser.city;
                carrierusers.state = carrierUser.state;
                carrierusers.zipcode = carrierUser.zipcode;
                carrierusers.telephone = carrierUser.telephone;
                carrierusers.fax = carrierUser.fax;
                carrierusers.status = carrierUser.status;
                carrierusers.factoryCompanyName = carrierUser.factoryCompanyName;
                carrierusers.factoryContactName = carrierUser.factoryContactName;
                carrierusers.factoryPhysicalAddress = carrierUser.factoryPhysicalAddress;
                carrierusers.factoryCity = carrierUser.factoryCity;
                carrierusers.factoryState = carrierUser.factoryState;
                carrierusers.factoryZipCode = carrierUser.factoryZipCode;
                carrierusers.factoryTelephone = carrierUser.factoryTelephone;
                carrierusers.factoryFax = carrierUser.factoryFax;
                carrierusers.additionalPersonName = carrierUser.additionalPersonName;
                carrierusers.addtionalPersonTelephone = carrierUser.addtionalPersonTelephone;
                carrierusers.addtionalAfterHoursPersonName = carrierUser.addtionalAfterHoursPersonName;
                carrierusers.addtionalAfterHoursPersonTelephone = carrierUser.addtionalAfterHoursPersonTelephone;
                carrierusers.addtionalDot = carrierUser.addtionalDot;
                carrierusers.additionalScac = carrierUser.additionalScac;
                carrierusers.additionalFedaralID = carrierUser.additionalFedaralID;
                carrierusers.additionalHazmatCertified = carrierUser.additionalHazmatCertified;
                carrierusers.additinalHazmatExpiryDate = carrierUser.additinalHazmatExpiryDate;
                carrierusers.additionalPreferredLanes = carrierUser.additionalPreferredLanes;
                carrierusers.additionalMajorMakets = carrierUser.additionalMajorMakets;
                carrierusers.authorizedSignature = carrierUser.authorizedSignature;
                carrierusers.authorizedSignaturePath = carrierUser.authorizedSignaturePath;
                carrierusers.completedMBCA = carrierUser.completedMBCA;
                carrierusers.reportSortKey = carrierUser.reportSortKey;
                carrierusers.ndaReturned = carrierUser.ndaReturned;
                carrierusers.onboardingCompleted = carrierUser.onboardingCompleted;
                carrierusers.onboardingFileLink = carrierUser.onboardingFileLink;
                carrierusers.insuranceType = carrierUser.insuranceType;
                carrierusers.paymentTerms = carrierUser.paymentTerms;
                carrierusers.majorMarkets = carrierUser.majorMarkets;
                carrierusers.CreatedDate = carrierUser.CreatedDate;
                carrierusers.createdBy = carrierUser.createdBy;
                carrierusers.ModifiedDate = carrierUser.ModifiedDate;
                carrierusers.ca = carrierUser.ca;
                carrierusers.isFemaleOwned = carrierUser.isFemaleOwned;
                carrierusers.isVeteranOwned = carrierUser.isVeteranOwned;
                carrierusers.modifiedBy = carrierUser.modifiedBy;
                carrierusers.assignee = carrierUser.assignee;
                carrierusers.isMinorityBusiness = carrierUser.isMinorityBusiness;
                carrierusers.isDeleted = carrierUser.isDeleted;
                carrierusers.businessYear = carrierUser.businessYear;
                carrierusers.ethnicIdentification = carrierUser.ethnicIdentification;
                carrierusers.milesPerYear = carrierUser.milesPerYear;
                carrierusers.paymentMethods = carrierUser.paymentMethods;
                carrierusers.CarrierwiseVehicle = carrierUser.CarrierwiseVehicle;
                carrierusers.CarrierwiseTrailer = carrierUser.CarrierwiseTrailer;
                carrierusers.CarrierDocument = carrierUser.CarrierDocument;
                carrierusers.CreatedUserName = carrierUser.CreatedUserName;
                carrierusers.serviceArea = carrierUser.serviceArea;
                carrierusers.brokerOptions = carrierUser.brokerOptions;
                carrierusers.createdByUserName = carrierUser.createdByUserName;
                carrierusers.cargoSpecification = carrierUser.cargoSpecification;
                carrierusers.Ins1MGeneral = carrierUser.Ins1MGeneral;
                carrierusers.Ins1MAuto = carrierUser.Ins1MAuto;
                carrierusers.Ins250KCargo = carrierUser.Ins250KCargo;
                carrierusers.Ins100KCargo = carrierUser.Ins100KCargo;
                //_jWLDBContext.carrierusers.Update(carrierusers);
                await _jWLDBContext.SaveChanges(UserID);

                if (carrierUser.CarrierwiseVehicle != null && carrierUser.CarrierwiseVehicle != "")
                {
                    List<vehicles> vehicles = JsonConvert.DeserializeObject<List<vehicles>>(carrierUser.CarrierwiseVehicle);

                    for (int i = 0; i < vehicles.Count; i++)
                    {
                        if (vehicles[i].id != null && vehicles[i].id != "" && vehicles[i].id.Trim() != "")
                        {
                            carriervehicle carrierVehicle = await _jWLDBContext.carriervehicle.Where(t => t.carrierVehicleId == Convert.ToInt32(vehicles[i].id)).FirstOrDefaultAsync();
                            carrierVehicle = new carriervehicle()
                            {
                                carrierVehicleId = Convert.ToInt32(vehicles[i].id),
                                carrierId = carrierUser.cuId,
                                numberOfVehicle = Convert.ToInt32(vehicles[i].numberOfVehicle),
                                vehicleId = Convert.ToInt32(vehicles[i].selectedVehicle)
                            };
                            await _jWLDBContext.SaveChanges(UserID);
                        }
                        else
                        {
                            carriervehicle vehicltype = new carriervehicle()
                            {
                                carrierId = carrierUser.cuId,
                                numberOfVehicle = Convert.ToInt32(vehicles[i].numberOfVehicle),
                                vehicleId = Convert.ToInt32(vehicles[i].selectedVehicle)
                            };
                            await _jWLDBContext.carriervehicle.AddAsync(vehicltype);
                            await _jWLDBContext.SaveChanges(UserID);
                        }
                    }
                }
                if (carrierUser.CarrierwiseTrailer != null && carrierUser.CarrierwiseTrailer != "")
                {
                    List<tempTrailer> trailers = JsonConvert.DeserializeObject<List<tempTrailer>>(carrierUser.CarrierwiseTrailer);

                    for (int i = 0; i < trailers.Count; i++)
                    {
                        if (trailers[i].id != null && trailers[i].id != "" && trailers[i].id.Trim() != "")
                        {
                            carrierTrailer trailetype = await _jWLDBContext.carrierTrailer.Where(t => t.carrierTrailerId == Convert.ToInt32(trailers[i].id)).FirstOrDefaultAsync();
                            trailetype = new carrierTrailer()
                            {
                                carrierTrailerId = Convert.ToInt32(trailers[i].id),
                                carrierId = carrierUser.cuId,
                                numberOfVehicle = Convert.ToInt32(trailers[i].numberOfVehicle),
                                trailerId = Convert.ToInt32(trailers[i].selectedTrailer)
                            };
                            await _jWLDBContext.SaveChanges(UserID);
                        }
                        else
                        {
                            carrierTrailer trailetype = new carrierTrailer()
                            {
                                carrierId = carrierUser.cuId,
                                numberOfVehicle = Convert.ToInt32(trailers[i].numberOfVehicle),
                                trailerId = Convert.ToInt32(trailers[i].selectedTrailer)
                            };
                            await _jWLDBContext.carrierTrailer.AddAsync(trailetype);
                            await _jWLDBContext.SaveChanges(UserID);
                        }
                    }
                }
                if (carrierUser.CarrierDocument != null && carrierUser.CarrierDocument != "")
                {
                    List<attachmentPath> paths = JsonConvert.DeserializeObject<List<attachmentPath>>(carrierUser.CarrierDocument);
                    for (int i = 0; i < paths.Count; i++)
                    {
                        if (paths[i].id != null && paths[i].id != "" && paths[i].id.Trim() != "")
                        {
                            authorizedpath authorizedpath = new authorizedpath()
                            {
                                authorizedId = Convert.ToInt32(paths[i].id),
                                carrierId = carrierUser.cuId,
                                documentPath = paths[i].url,
                                selectedOptions = paths[i].selectedVal
                            };
                            await _jWLDBContext.SaveChanges(UserID);
                        }
                        else
                        {
                            authorizedpath authorizedpath = new authorizedpath()
                            {
                                carrierId = carrierUser.cuId,
                                documentPath = paths[i].url,
                                selectedOptions = paths[i].selectedVal
                            };
                            await _jWLDBContext.authorizedpath.AddAsync(authorizedpath);
                            await _jWLDBContext.SaveChanges(UserID);
                        }

                    }
                }



            }
            catch (Exception ex)
            {

                _jWLDBContext.errortracelog.Add(new errortracelog()
                {
                    error = ex.ToString(),
                    errorControl = "CarrierService/EditCarrierUser",
                    errorMessage = ex.Message,
                    errorName = ex.Source,
                    errorStack = ex.StackTrace,
                    errorTime = DateTime.Now
                });
                await _jWLDBContext.SaveChangesAsync();
            }

        }
    }
    public class vehicles
    {
        public string id { get; set; }
        public string selectedVehicle { get; set; }
        public string numberOfVehicle { get; set; }
        public vehicltype vehicltype { get; set; }
    }
    public class tempTrailer
    {
        public string id { get; set; }
        public string selectedTrailer { get; set; }
        public string numberOfVehicle { get; set; }
        public trailer trailer { get; set; }
    }
    public class attachmentPath
    {
        public string id { get; set; }
        public string url { get; set; }
        public string selectedVal { get; set; }
    }
}

