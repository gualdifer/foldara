namespace Foldara.Engine.Tests;

using Xunit;

public sealed class AssemblyMarkerTests
{
    [Fact]
    public void MarkerCanBeConstructed()
    {
        var marker = new AssemblyMarker();

        Assert.NotNull(marker);
    }
}
