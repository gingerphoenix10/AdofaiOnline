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
    public byte ColorIndex;

    // DLC
    public bool ownsNeoCosmos; 
    public bool ownsVegaDLC; 
    public bool ownsFeaturedDLC;

    public PlayerInfo(byte PlayerID, bool ownsNeoCosmos, bool ownsVegaDLC, bool ownsFeaturedDLC)
    {
        this.PlayerID = PlayerID;
        this.ownsNeoCosmos = ownsNeoCosmos;
        this.ownsVegaDLC = ownsVegaDLC;
        this.ownsFeaturedDLC = ownsFeaturedDLC;
    }

    public byte[] Serialize()
    {
        return new byte[]
        {
            PlayerID,
            ColorIndex,
            Convert.ToByte(ownsNeoCosmos),
            Convert.ToByte(ownsVegaDLC),
            Convert.ToByte(ownsFeaturedDLC)
        };
    }

    public static PlayerInfo Deserialize(byte[] data)
    {
        return new PlayerInfo(data[0], Convert.ToBoolean(data[2]), Convert.ToBoolean(data[3]), Convert.ToBoolean(data[4]))
        {
            PlayerID = data[0],
            ColorIndex = data[1],
            ownsNeoCosmos = Convert.ToBoolean(data[2]),
            ownsVegaDLC = Convert.ToBoolean(data[3]),
            ownsFeaturedDLC = Convert.ToBoolean(data[4])
        };
    }
}