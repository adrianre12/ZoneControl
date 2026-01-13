using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.Text;
using VRage.Utils;
using VRageMath;
using ZoneControl.Spawner;

namespace ZoneControl.NotificationBlock
{
    internal class ScreenNotification : ScreenBase
    {
        private const long TicksPerSecond = 10000000L;
        private const float SecondsPerTick = 1.0f / TicksPerSecond;


        private readonly Color GreenCRT = new Color(51, 255, 0);

        //private Stopwatch stopwatch = new Stopwatch();

        internal bool Dirty;

        internal ScreenNotification() { }
        internal ScreenNotification(IMyTextSurfaceProvider surfaceProvider, int index)
        {
            base.Init(surfaceProvider, index);
            DefaultRotationOrScale = 1.5f;
            BackgroundColor = Color.MidnightBlue;
        }

        internal void Refresh(List<SummaryItem> selected)
        {
            var frame = GetFrame(BackgroundColor);
            var positionTop = new Vector2(5, 5);
            var positionList = new Vector2(5, 80);
            var positionBtm = new Vector2(5, 455);

            var positionTab1 = new Vector2(300, 0);

            /*
            for (int x = 0; x < viewport.Width; x += 50)
                frame.Add(NewTextSprite("_", new Vector2(position.X + x, position.Y)));
            for (int y = 0; y < 20; ++y)
            {
                frame.Add(NewTextSprite($"{y}", position));
                position.Y += LineSpaceing;
            }
            */

            frame.Add(NewTextSprite("Scanning for Anomalies:", positionTop));


            long now = DateTime.Now.Ticks;
            var sb = new StringBuilder();

            foreach (var item in selected)
            {

                sb.Clear();
                MyValueFormatter.AppendTimeInBestUnit((item.RemoveAt - now) * SecondsPerTick, sb);
                Log.Msg($"({item.RemoveAt - now} {SecondsPerTick} time ={sb.ToString()}");
                frame.Add(NewTextSprite(item.Name, positionList));
                frame.Add(NewTextSprite(sb.ToString(), positionTab1 + positionList));

                positionList.Y += 125;
            }

            frame.Add(NewTextSprite("Click button to save GPS", positionBtm));
            //frame.Add(NewTextSprite($"{RunInfo.AvailableUranium}", position + positionTab1, Color.Green));

            Dirty = false;
            frame.Dispose();
        }
    }
}
