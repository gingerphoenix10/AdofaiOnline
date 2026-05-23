using Steamworks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AdofaiOnline;

public class PlayerInfo
{
    public byte PlayerID;

    // DLC
    // I'm not even gonna be bothered to implement these yet, especially since Neo Cosmos doesn't allow co-op yet
    public bool ownsNeoCosmos; 
    public bool ownsVegaDLC; 
    public bool ownsFeaturedDLC;

    public PlayerInfo(byte PlayerID)
    {
        this.PlayerID = PlayerID;
    }
}