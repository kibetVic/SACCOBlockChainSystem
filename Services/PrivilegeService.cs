
using Microsoft.EntityFrameworkCore;
using SACCOBlockChainSystem.Data;
using SACCOBlockChainSystem.Models;
using SACCOBlockChainSystem.Models.ViewModels;

namespace SACCOBlockChainSystem.Services
{

    public class PrivilegeService
    {
        private readonly ApplicationDbContext _context;
        public PrivilegeService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<List<PrivilegeViewModel>> GetAllPrivilegesAsync()
        {

            return MapToPrivilegeViewModel(await _context.GroupRights.ToListAsync());

        }
        public async Task<bool> AddPrivilegeAsync(Usergrp newPrivilege)
        {


            // Check if the privilege already exists
            var existingPrivilege = await _context.GroupRights
                .FirstOrDefaultAsync(p => p.Feature == newPrivilege.Feature);

            if (existingPrivilege != null)
            {
                return false;
            }

            // Add the new privilege to the database
            _context.GroupRights.Add(newPrivilege);
            await _context.SaveChangesAsync();
            return true;
        }
        public List<PrivilegeViewModel> MapToPrivilegeViewModel(List<Usergrp> privileges)
        {
            return privileges.Select(p => new PrivilegeViewModel
            {
                Id = p.RightId,
                Name = p.Feature,
                IsSelected = false // Initially set to false, can be updated based on logic
            }).ToList();
        }
    }
}
