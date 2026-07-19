using FAP.Network;
using Xunit;

namespace FAP.UnitTests;

public class MultiplexorTests
{
    [Fact]
    public void Encode_includes_verb_and_base64_param()
    {
        var url = Multiplexor.Encode("127.0.0.1:8030", "BROWSE", "/Tmp");
        Assert.StartsWith("http://127.0.0.1:8030/Fap.app/BROWSE", url);
        Assert.Contains("?p=", url);
    }

    [Fact]
    public void Encode_without_param_omits_query()
    {
        var url = Multiplexor.Encode("http://127.0.0.1:40/", "INFO", string.Empty);
        Assert.Equal("http://127.0.0.1:40/Fap.app/INFO", url);
    }
}