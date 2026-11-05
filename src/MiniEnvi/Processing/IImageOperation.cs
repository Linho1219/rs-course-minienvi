using MiniEnvi.IO;

namespace MiniEnvi.Processing;

public interface IImageOperation
{
    string Name { get; }
    Task ExecuteAsync(EnviDataset dataset, CancellationToken cancellationToken = default);
}
