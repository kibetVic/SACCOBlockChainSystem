using SACCOBlockChainSystem.Models;
using SACCOBlockChainSystem.Models.DTOs;

namespace SACCOBlockChainSystem.Services
{
    public interface IMemberService
    {
        Task<MemberResponseDTO> RegisterMemberAsync(MemberRegistrationDTO registration);
        Task<Member> GetMemberByMemberNoAsync(string memberNo);
        Task<List<Member>> SearchMembersAsync(string searchTerm);
        Task<decimal> GetMemberShareBalanceAsync(string memberNo);
        Task<bool> UpdateMemberAsync(string memberNo, Member member);
        Task<decimal> GetShareBalanceAsync(string memberNo);
        Task<List<BlockchainTransaction>> GetMemberBlockchainHistoryAsync(string memberNo);
        Task<List<Member>> GetAllMembersAsync();
    }
}