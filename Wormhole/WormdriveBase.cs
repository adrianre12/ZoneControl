using Sandbox.ModAPI;
using System.Collections.Generic;

namespace ZoneControl.Wormhole
{
    internal abstract class WormdriveBase : ZoneControlBase
    {
        internal static Dictionary<long, IMyFunctionalBlock> driveRegister = new Dictionary<long, IMyFunctionalBlock>();
        internal static Dictionary<long, IMyFunctionalBlock> chargerRegister = new Dictionary<long, IMyFunctionalBlock>();
    }
}
