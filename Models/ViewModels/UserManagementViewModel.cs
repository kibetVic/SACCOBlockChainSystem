// Models/ViewModels/UserManagementViewModel.cs
using SACCOBlockChainSystem.Models.DTOs;

namespace SACCOBlockChainSystem.Models.ViewModels
{
    public class UserManagementViewModel
    {
        public List<UserListDTO> Users { get; set; } = new List<UserListDTO>();
        public string SearchTerm { get; set; }
        public bool IsEditMode { get; set; }
        public UserDTO SelectedUser { get; set; }
        public string? UserName { get; set; }
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public string? UserGroup { get; set; }
        public string? CompanyCode { get; set; }
        public string? Status { get; set; }
        public string? Department { get; set; }
        public int? SubCountyId { get; set; }
        public int? WardId { get; set; }
        public List<PendingUserDTO> PendingUsers { get; set; } = new();
        public List<LockedUserDTO> LockedUsers { get; set; } = new();
        public int PendingCount { get; set; }
        public int LockedCount { get; set; }


    }

    public class UserListDTO
    {
        public int UserId { get; set; }
        public string? UserName { get; set; }
        public string? UserLoginId { get; set; }
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public string? Department { get; set; }
        public string? UserGroup { get; set; }
        public string? CompanyCode { get; set; }
        public string? CompanyName { get; set; }
        public string? Status { get; set; }
        public bool IsLocked { get; set; }
    }

    public class PendingUserDTO
    {
        public int UserId { get; set; }
        public string UserName { get; set; } = null!;
        public string UserLoginId { get; set; } = null!;
        public string Email { get; set; } = null!;
        public string Phone { get; set; } = null!;
        public string Department { get; set; } = null!;
        public string UserGroup { get; set; } = null!;
        public string CompanyCode { get; set; } = null!;
        public string CompanyName { get; set; } = null!;
        public DateTime DateCreated { get; set; }
        public string SubCounty { get; set; } = null!;
        public string Ward { get; set; } = null!;
    }

    public class LockedUserDTO
    {
        public int UserId { get; set; }
        public string UserName { get; set; } = null!;
        public string UserLoginId { get; set; } = null!;
        public string Email { get; set; } = null!;
        public string Phone { get; set; } = null!;
        public string Department { get; set; } = null!;
        public string UserGroup { get; set; } = null!;
        public string CompanyCode { get; set; } = null!;
        public string CompanyName { get; set; } = null!;
        public int FailedAttempts { get; set; }
        public DateTime? LockedDate { get; set; }
        public string LockReason { get; set; } = "Too many failed login attempts";
        public bool IsLocked { get; set; } = true;  
        public string? Status { get; set; } 
    }
}