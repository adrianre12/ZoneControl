using Sandbox.ModAPI;
using VRage.Game.GUI.TextPanel;
using VRageMath;

namespace ZoneControl.NotificationBlock
{
    internal abstract class ScreenBase
    {
        private IMyTextSurface surface;
        internal RectangleF viewport;

        internal SpriteType DefaultType = SpriteType.TEXT;
        internal float DefaultRotationOrScale = 1f;  // FontId White Scale 1f is 20px high
        internal Color DefaultColor = Color.White;
        internal TextAlignment DefaultAlignment = TextAlignment.LEFT;
        internal string DefaultFontId = "White";
        internal Color BackgroundColor = Color.Black;
        internal int LineSpaceing = 25;

        public ScreenBase() { }

        protected virtual void Init(IMyTextSurfaceProvider surfaceProvider, int index)
        {
            surface = (IMyTextSurface)surfaceProvider.GetSurface(index);
            viewport = new RectangleF((surface.TextureSize - surface.SurfaceSize) / 2f, surface.SurfaceSize);
            surface.ContentType = ContentType.SCRIPT;
            surface.Script = "";
        }

        /*        public MySpriteDrawFrame GetFrame()
                {
                    return GetFrame(BackgroundColor);
                }*/

        public MySpriteDrawFrame GetFrame(Color color)
        {
            //            surface.ContentType = ContentType.SCRIPT;
            //            surface.Script = "";
            var frame = surface.DrawFrame();
            frame.Add(new MySprite() //Background
            {
                Type = SpriteType.TEXTURE,
                Data = "White screen",
                Position = viewport.Center,
                Size = viewport.Size,
                Color = color,
                Alignment = TextAlignment.CENTER
            });
            return frame;
        }

        internal MySprite NewTextSprite(string text, Vector2 position)
        {
            //return NewTextSprite(text, position, DefaultColor, DefaultRotationOrScale, DefaultAlignment, DefaultFontId);
            return new MySprite()
            {
                Type = DefaultType,
                Data = text,
                Position = position + viewport.Position,
                RotationOrScale = DefaultRotationOrScale,
                Color = DefaultColor,
                Alignment = DefaultAlignment,
                FontId = DefaultFontId
            };
        }

        /*        internal MySprite NewTextSprite(string text, Vector2 position, float scale)
                {
                    //return NewTextSprite(text, position, DefaultColor, scale, DefaultAlignment, DefaultFontId);
                    return new MySprite()
                    {
                        Type = DefaultType,
                        Data = text,
                        Position = position + viewport.Position,
                        RotationOrScale = scale,
                        Color = DefaultColor,
                        Alignment = DefaultAlignment,
                        FontId = DefaultFontId
                    };
                }*/
        /*        internal MySprite NewTextSprite(string text, Vector2 position, Color color)
                {
                    //return NewTextSprite(text, position, color, DefaultRotationOrScale, DefaultAlignment, DefaultFontId);
                    return new MySprite()
                    {
                        Type = DefaultType,
                        Data = text,
                        Position = position + viewport.Position,
                        RotationOrScale = DefaultRotationOrScale,
                        Color = color,
                        Alignment = DefaultAlignment,
                        FontId = DefaultFontId
                    };
                }*/

        /*        internal MySprite NewTextSprite(string text, Vector2 position, float scale, TextAlignment alignment)
                {
                    //return NewTextSprite(text, position, DefaultColor, scale, alignment, DefaultFontId);
                    return new MySprite()
                    {
                        Type = DefaultType,
                        Data = text,
                        Position = position + viewport.Position,
                        RotationOrScale = scale,
                        Color = DefaultColor,
                        Alignment = alignment,
                        FontId = DefaultFontId
                    };
                }*/
        /*        internal MySprite NewTextSprite(string text, Vector2 position, Color color, float scale, TextAlignment alignment, string fontId)
                {
                    return new MySprite()
                    {
                        Type = DefaultType,
                        Data = text,
                        Position = position + viewport.Position,
                        RotationOrScale = scale,
                        Color = color,
                        Alignment = alignment,
                        FontId = fontId
                    };
                }*/
    }

    // How to use
    // Create a class like ScreenTest
    // In UpdateOnceBeforeFrame()
    //      screen0 = new ScreenTest();
    //      screen0.Init((IMyTextSurfaceProvider) block, 0);
    // In UpdateAfterSimulation100()
    //      screen0.TestScreen(13,"Hello World");

    // Example of how to set up a Screen class in another file.
    // using Sandbox.ModAPI;
    // using VRageMath;

    /*    internal class ScreenTest : ScreenBase
        {
            public ScreenTest() { }

            //Only needed if you wan to do setup
            public override void Init(IMyTextSurfaceProvider surfaceProvider, int index)
            {
                base.Init(surfaceProvider, index);
                // default setup
                DefaultRotationOrScale = 0.85f;
                //etc
            }

            // Create methods to draw your screens
            public void TestScreen(int lines, string msg)
            {
                var frame = GetFrame();
                var position = new Vector2(0, 0);
                int height = LineSpaceing;
                DefaultRotationOrScale = 0.85f;
                for (int i = 1; i < lines; i++)
                {
                    frame.Add(NewTextSprite($"[{i} position={position.ToString()} '{msg}']", position));
                    position += new Vector2(0, height);
                }
            }
        }*/
}
