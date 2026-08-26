using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using SACCOBlockChainSystem.Data;
using SACCOBlockChainSystem.Models;

public class LoanRepaymentService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;

    public LoanRepaymentService(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await ProcessRepayments();
            await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
        }
    }

    private async Task ProcessRepayments()
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var today = DateTime.Today;

        var dueSchedules = await context.LoanSchedules
            .Where(x => x.Status == "Pending" && x.DueDate <= today)
            .ToListAsync();

        foreach (var item in dueSchedules)
        {
            var repayment = new Repay
            {
                LoanNo = item.LoanNo,
                Amount = item.TotalInstallment,
                Principal = item.PrincipalAmount,
                Interest = item.InterestAmount,
                DateReceived = DateTime.Now,
                UserName = "SYSTEM"
            };

            context.Repay.Add(repayment);

            item.Status = "Paid";
        }

        await context.SaveChangesAsync();
    }
}