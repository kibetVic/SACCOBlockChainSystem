using SACCOBlockChainSystem.Models.DTOs;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SACCOBlockChainSystem.Services
{
    public interface ILoanTypeService
    {
        Task<LoanTypeResponseDTO> CreateLoanTypeAsync(LoanTypeCreateDTO loanTypeDto);
        Task<LoanTypeResponseDTO> UpdateLoanTypeAsync(string loanCode, LoanTypeUpdateDTO loanTypeDto);
        Task<bool> DeleteLoanTypeAsync(string loanCode, string companyCode);
        Task<LoanTypeResponseDTO> GetLoanTypeByCodeAsync(string loanCode, string companyCode);
        Task<List<LoanTypeResponseDTO>> GetLoanTypesByCompanyAsync(string companyCode);
        Task<List<LoanTypeSimpleDTO>> GetActiveLoanTypesAsync(string companyCode);
        Task<List<LoanTypeResponseDTO>> SearchLoanTypesAsync(string searchTerm, string companyCode);
        Task<bool> ValidateLoanTypeAsync(LoanTypeCreateDTO loanTypeDto);
        Task<int> GetLoanTypeUsageCountAsync(string loanCode, string companyCode);
        Task<List<LoanTypeSimpleDTO>> GetLoanTypesForMemberAsync(string memberNo, string companyCode);
        Task<dynamic> GetAllLoanTypesAsync(string companyCode);
    }
}