using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AdofaiOnline;

public enum PacketType : byte
{
    Welcome = 0x00,
    Update = 0x01,
    CountChanged = 0x02,
    Die = 0x03,
    ChangeScene = 0x04,
    Revive = 0x05,
    GetReady = 0x06,
    SetLevel = 0x07,
    Pause = 0x08,
    Damage = 0x09
}