using Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Interfaces
{
    public interface ICarrierService
    {
        Task<List<carrierusers>> GetCarrierUsers();
        Task<List<carrierusers>> ExportCarrierUsers();
        Task AddCarrierUser(carrierusers carrierUser, string UserID);
        Task UpdateURL(carrierusers carrierusers, string UserID);
        Task<carrierusers> GetCarrierusersById(int id);
        Task EditCarrierUser(carrierusers carrierUser, string UserID);
        Task<string> CheckEmailDOT(string Email, string DOT, bool IsEdit, int id = 0);
        Task<string> CheckDOT(string DOT, bool IsEdit, int id = 0);
        Task<string> CheckEmail(string Email, bool IsEdit, int id = 0);
        Task DeleteAttachment(string File, string UserID);
        Task DeleteVehicle(int VehicleID, string UserID);
        Task DeleteTrailer(int TrailerID, string UserID);
    }
}
