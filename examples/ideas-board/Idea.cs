namespace IdeasBoard;

// tag::entity[]
public class Idea
{
    public Guid Id { get; set; }

    public string Text { get; set; } = "";

    public int Votes { get; set; }
}
// end::entity[]
