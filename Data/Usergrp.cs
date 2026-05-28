using SACCOBlockChainSystem.Models;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace SACCOBlockChainSystem.Data
{
    public partial class Usergrp
    {
        [Key]
        public int RightId { get; set; }
        public string Feature { get; set; }
        public int GroupId { get; set; }
        public bool Value { get; set; }
        public List<RolePrivilege> RolePrivileges { get; set; }
        //public DateTime? AuditTime { get; set; }
    }

    public class RolePrivilege
    {
        public int GroupId { get; set; }
        [ForeignKey("GroupId")]
        public UserGroup? Usergroup { get; set; }
        public int RightId { get; set; }
        [ForeignKey("RightId")]
        public Usergrp? Usergrp { get; set; }

    }
}