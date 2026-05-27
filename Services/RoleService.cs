namespace SACCOBlockChainSystem.Services;

using global::SACCOBlockChainSystem.Data;
using global::SACCOBlockChainSystem.Models;
using global::SACCOBlockChainSystem.Models.ViewModels;
using Microsoft.EntityFrameworkCore;
//using SACCOBlockChainSystem.Data;



public class RoleService
{

    //private readonly ApplicationDbContext _mcontext;
    private readonly ApplicationDbContext _context;
    // private readonly BosaDbContext _db;
    private readonly PrivilegeService _privilegeService;
    private readonly IHttpContextAccessor httpContextAccessor;
    public RoleService(ApplicationDbContext context, PrivilegeService privilegeService)
    {
        //_mcontext = mcontext;
        _context = context;
        //_db = db;
        _privilegeService = privilegeService;
    }
    public async Task<List<UserGroup>> GetRolesAsync()
    {

        List<UserGroup> userRoles = await _context.UserGroups.ToListAsync();
        return userRoles;
    }

    // Get privileges assigned to a specific role
    public async Task<List<PrivilegeViewModel>> GetPrivilegesForRoleAsync(int roleId)
    {

        try
        {

            // Get only the privileges that are assigned to the role
            var assignedPrivileges = await _context.GroupRights
                .Where(p => _context.RolePrivileges
                    .Where(rp => rp.GroupId == roleId)
                    .Select(rp => rp.RightId)
                    .Contains(p.RightId))
                .ToListAsync();

            // Map only the assigned privileges to ViewModel
            var privilegeViewModels = _privilegeService.MapToPrivilegeViewModel(assignedPrivileges);

            return privilegeViewModels;
        }
        catch (Exception ex)
        {
            return new List<PrivilegeViewModel>();
        }

    }


    // Add a privilege to a role
    public async Task AddPrivilegeToRoleAsync(int roleId, int privilegeId)
    {

        var rolePrivilege = new RolePrivilege
        {
            GroupId = roleId,
            RightId = privilegeId
        };

        _context.RolePrivileges.Add(rolePrivilege);
        await _context.SaveChangesAsync();
    }

    // Remove a privilege from a role
    public async Task RemovePrivilegeFromRoleAsync(int roleId, int privilegeId)
    {
        try
        {

            var rolePrivilege = await _context.RolePrivileges.FirstOrDefaultAsync(rp => rp.GroupId == roleId && rp.RightId == privilegeId);
            if (rolePrivilege != null)
            {
                _context.RolePrivileges.Remove(rolePrivilege);
                await _context.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            //ToastService.Notify(new(ToastType.Danger, $"Error: {ex.Message}."));
        }

    }
}



