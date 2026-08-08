using SACCOBlockChainSystem.Models;
using SACCOBlockChainSystem.Models.DTOs;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SACCOBlockChainSystem.Services
{
    public interface IMemberService
    {
        Task<MemberResponseDTO> RegisterMemberAsync(MemberRegistrationDTO registration);        
        Task<List<Member>> SearchMembersAsync(string searchTerm);
        Task<List<BlockchainTransaction>> GetMemberBlockchainHistoryAsync(string memberNo);
        Task<List<Member>> GetAllMembersAsync();
        Task<decimal> GetShareBalanceAsync(string memberNo);
        Task<MemberDTO> GetMemberDetailsAsync(string memberNo);        
        Task<MemberResponseDTO> UpdateMemberAsync(string memberNo, MemberUpdateDTO updateDto);
        Task<decimal> GetMemberShareTypeTotalAsync(string memberNo, string shareTypeCode, string companyCode);
        Task<MembersPerCIGReportViewModel> GetMembersPerCIGReportAsync(string companyCode, string? searchTerm = null, string? statusFilter = null);
    }
}