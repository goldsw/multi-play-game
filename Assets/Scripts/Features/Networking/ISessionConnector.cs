using System.Threading.Tasks;

namespace MushroomSurvival
{
    public interface ISessionConnector
    {
        Task<string> CreateAsync();
        Task JoinAsync(string code);
        Task LeaveAsync();
    }
}
