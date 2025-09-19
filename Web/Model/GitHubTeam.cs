namespace Web.Model
{
    public class GitHubTeam
    {
        public long Id { get; set; }
        public string EntraId { get; set; }
        public IEnumerable<GraphUserDto> Members { get; set; }
    }
}
