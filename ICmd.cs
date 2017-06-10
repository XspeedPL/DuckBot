
namespace DuckBot
{
    interface ICmd
    {
        string Run(CmdContext ctx);
    }
}
