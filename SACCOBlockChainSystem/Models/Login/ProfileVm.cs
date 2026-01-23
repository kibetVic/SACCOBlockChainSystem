// Models/ViewModels/ProfileVm.cs
namespace SACCOBlockChainSystem.Models.ViewModels
{
    public class ProfileVm
    {
        public int UserId { get; set; }
        public string UserName { get; set; }
        public string UserLoginId { get; set; }
        public string Email { get; set; }
        public string Phone { get; set; }
        public string MemberNo { get; set; }
        public string Department { get; set; }
        public string SubCounty { get; set; }
        public string Ward { get; set; }
        public string UserGroup { get; set; }
        public string Status { get; set; }
        public DateTime? DateCreated { get; set; }
    }
}