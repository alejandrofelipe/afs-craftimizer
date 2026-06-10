namespace Craftimizer.UIStudio;

public interface IStory
{
    string Category { get; }
    string Name { get; }
    void Draw();
}
