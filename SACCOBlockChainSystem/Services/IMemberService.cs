using SACCOBlockChainSystem.Models;
using SACCOBlockChainSystem.Models.DTOs;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SACCOBlockChainSystem.Services
{
    public interface IMemberService
    {
        Task<MemberResponseDTO> RegisterMemberAsync(MemberRegistrationDTO registration);
        Task<Member> GetMemberByMemberNoAsync(string memberNo);
        Task<List<Member>> SearchMembersAsync(string searchTerm);
        Task<decimal> GetMemberShareBalanceAsync(string memberNo);
        Task<bool> UpdateMemberAsync(string memberNo, Member updatedMember);
        Task<List<BlockchainTransaction>> GetMemberBlockchainHistoryAsync(string memberNo);
        Task<List<Member>> GetAllMembersAsync();
        Task<decimal> GetShareBalanceAsync(string memberNo);
        Task<MemberDTO> GetMemberDetailsAsync(string memberNo);
    }


    //public interface IMemberService
    //{
    //    // Member CRUD operations
    //    Task<Member> GetMemberByMemberNoAsync(string memberNo, string? userCompanyCode);
    //    Task<Member> GetMemberByIdAsync(int id);
    //    Task<MemberDTO> GetMemberDetailsAsync(string memberNo);
    //    Task<List<MemberDTO>> SearchMembersAsync(string searchTerm, string userCompanyCode);
    //    Task<List<Member>> GetAllMembersAsync(string userCompanyCode);
    //    Task<Member> RegisterMemberAsync(MemberRegistrationDTO registration);
    //    Task<Member> UpdateMemberAsync(string memberNo, MemberUpdateDTO updateDto);
    //    Task<bool> DeactivateMemberAsync(string memberNo);

    //    // Financial operations
    //    Task<decimal> GetMemberBalanceAsync(string memberNo);
    //    Task<decimal> GetMemberShareBalanceAsync(string memberNo);
    //    Task<decimal> GetMemberLoanBalanceAsync(string memberNo);

    //    // Blockchain operations
    //    Task<List<BlockchainTransaction>> GetMemberBlockchainHistoryAsync(string memberNo);

    //    // Validation operations
    //    Task<bool> MemberExistsAsync(string memberNo);
    //    Task<bool> IdNumberExistsAsync(string idNumber);

    //    // Reporting operations
    //    Task<MemberSummaryDTO> GetMemberSummaryAsync(string memberNo);
    //    Task<List<MemberTransactionSummary>> GetMemberTransactionSummaryAsync(string memberNo, DateTime? startDate, DateTime? endDate);
    //    Task<bool> UpdateMemberAsync(string memberNo, Member updatedMember);
    //    Task<IEnumerable<object>> SearchMembersAsync(string searchTerm);
    //    Task<dynamic> GetMemberByMemberNoAsync(string memberNo);
    //}
}