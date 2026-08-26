using System.ComponentModel.DataAnnotations.Schema;

namespace SACCOBlockChainSystem.Models
{
    public class ApiSimulate
    {

        public string? companycode { get; set; }

        public decimal? amount { get; set; }
        public string BASE_URL { get; set; } = "https://easysacco.amtech.co.ke:8049";
        public string? ApiKey { get; set; } = "BVmY1Ufl8FeazdlKnWQ5e/hgUN8p/+dapzDagzNL1eCRAOhW67X0risDPOxZdVv+pVHKB7Oi3vsb/skOlxDZjPaW36i6A8n9+xleI6zsyNO1jT0SO9+h5mtZNK5ur7NeZK0gUdJfAGCANbCxzeuZo5PcAfPVfdhFUSuGvfU2nPxpD2dREAE/xuA85XVBdwlwRKCteNbpnLABgaHhfJPYwgTBu+aqLYYNZODBegwyHthTauvCSKVnb1BYgbrsrf34GlrcV7jEZQBMxFoiN2E4n72Mm/z2SeVGhCFGml0bOq8WQTlBC8p9ifT2TcmfFPZ3bv+sd8U0niRGKYfyw9BATg==";

        public string? phone { get; set; }
        public string? description { get; set; }
        public string apiUrl { get; set; } = "https://easysacco.amtech.co.ke:9090/ApisController/BULK";
        public string? reference { get; set; } = "https://easysacco.amtech.co.ke:8049/api/transactions/run?module=ussd";
        public string? remarks { get; set; }
        public string? occassion { get; set; }
        public string? action { get; set; }
        public string? conversationID { get; set; }
        public class UserInfo
        {
            public string Name { get; set; }
            public string Account { get; set; }
            [NotMapped]
            public string? Email { get; set; }
            public string Phone { get; set; }
            public decimal Amount { get; set; }
            public DateTime Date { get; set; }
        }
        public List<UserInfo>? Users { get; set; } = new List<UserInfo>();
        public int? app_id { get; set; }
        public string? simulateUrl { get; set; } = "https://easysacco.amtech.co.ke:9090/ApisController/Simulate";
    }

}
