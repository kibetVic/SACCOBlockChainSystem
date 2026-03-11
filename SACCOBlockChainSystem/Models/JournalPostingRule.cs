public class JournalPostingRule
{
    public int Id { get; set; }
    public string Keyword { get; set; } = "";
    public string DebitAccount { get; set; } = "";
    public string CreditAccount { get; set; } = "";
    public bool IsActive { get; set; }
}