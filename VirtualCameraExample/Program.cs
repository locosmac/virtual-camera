using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

// This namespace is from LocosLab.VirtualCamera.dll, which you can find
// in the global assembly cache in the LocosLab.VirtualCamera folder.
//
// %windir%\Microsoft.NET\assembly\GAC_MSIL\LocosLab.VirtualCamera
//
// In your project explorer, go to references, select add and then press
// the search button to navigate to the folder. Then find the file called
// LocosLab.VirtualCamera.dll and press ok. 
using LocosLab.VirtualCamera;

namespace VirtualCameraExample
{

    /// <summary>
    /// A console application that runs a virtual camera generating a simple animation.
    /// 
    /// Important: This program will load native code which will only work if the
    ///     architecture matches. Make sure to uncheck "prefer 32-bit" build 
    ///     option, since this will lead to mismatches on 64-bit systems.
    /// </summary>
    internal class Program
    {
        // set the COM threading model to multi-threaded (required for media foundation)
        [MTAThread] 
        static void Main(string[] args)
        {
            Console.WriteLine("This program demonstrates how to create a virtual camera in C#.");
            Console.WriteLine("Press <Enter> to start a virtual camera.");
            Console.ReadLine();
            // set the (human-facing) camera name as well as the frame size in pixels (1080p)
            VirtualCamera camera = new VirtualCamera("C# VCam", 1920, 1080, new ExampleFrameGeneratorFactory());
            uint result = 0;
            if ((result = camera.Start()) == 0)
            {
                Console.WriteLine("Camera started successfully, you can now use it in other programs.");
                Console.WriteLine("Press <Enter> to stop the virtual camera.");
                Console.ReadLine();
                if ((result = camera.Stop()) == 0)
                {
                    Console.WriteLine("Camera stopped successfully, it is now no longer available.");
                }
                else
                {
                    Console.WriteLine($"Failed to stop camera. Error code: {result:x}");
                }
            }
            else
            {
                Console.WriteLine($"Failed to start camera. Error code {result:x}");
            }
            Console.WriteLine("Press <Enter> to to close the application.");
            Console.ReadLine();
        }
    }

    /// <summary>
    /// The factory class generates our frame generator when
    /// the camera connects to the c# program. If the Windows
    /// Frame Server service is restarted (e.g. to handle hardware
    /// issues), it is possible that we need multiple frame generators.
    /// </summary>
    public class ExampleFrameGeneratorFactory : FrameGeneratorFactory
    {
        /// <summary>
        /// Log something to the console and return a generator.
        /// </summary>
        /// <param name="width">The width of the frame.</param>
        /// <param name="height">The height of the frame.</param>
        /// <returns>The generator.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if the protocol version does not match.</exception>
        public FrameGenerator CreateFrameGenerator(ushort width, ushort height)
        {
            Console.WriteLine($"Creating frame generator with {width}x{height} pixels.");
            return new ExampleFrameGenerator(width, height);
        }
    }


    /// <summary>
    /// A frame generator that creates a simple circular animation
    /// with some text in the center.
    /// </summary>
    public class ExampleFrameGenerator : FrameGeneratorBase
    {
        private Bitmap bitmap;
        private Graphics graphics;
        private byte[] bytes;
        private Pen pen;
        private int angle = 0;
        private Rectangle rect;
        private Rectangle circle;
        private Font font;
        private StringFormat format;

        /// <summary>
        /// Creates a frame generator for a particular frame size.
        /// </summary>
        /// <param name="width">The width in pixels.</param>
        /// <param name="height">The height in pixels.</param>
        public ExampleFrameGenerator(ushort width, ushort height)
        {
            // allocate everything that we need for drawing here
            // so that we avoid allocations during create frame
            bitmap = new Bitmap(width, height, PixelFormat.Format32bppRgb);
            graphics = Graphics.FromImage(bitmap);
            pen = new Pen(Color.White, 20);
            pen.DashCap = DashCap.Round;
            pen.Alignment = PenAlignment.Center;
            rect = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
            int min = Math.Min(bitmap.Width, bitmap.Height) / 2;
            int diffX = (bitmap.Width - min) / 2;
            int diffY = (bitmap.Height - min) / 2;
            circle = new Rectangle(diffX, diffY, min, min);
            format = new StringFormat();
            format.Alignment = StringAlignment.Center;
            format.LineAlignment = StringAlignment.Center;
            Font f = SystemFonts.DefaultFont;
            font = new Font(f.Name, min / 10, f.Style, f.Unit);
            // create a buffer to copy the image
            this.bytes = new byte[width * height * Constants.BYTES_PER_PIXEL];

        }

        /// <summary>
        /// This method generates a frame. It is called very often from the
        /// framework (since the camera aims for 30 fps). For the video not to 
        /// "hang", it is important to return the data quickly.
        /// </summary>
        /// <param name="time">The MFTime of the request.</param>
        /// <param name="writer">The writer to write the image data. The camera
        ///     expects the data in RGB32 format which is 1 32-byte value per 
        ///     pixel and 8-bits for the R, G, and B channel.</param>
        /// <param name="bytes">The number of bytes that you MUST write into
        ///     the writer before returning. A failure to do so will break the 
        ///     underlying serial protocol and you will not be able to recover 
        ///     from it.</param>
        public override void CreateFrame(long time, BinaryWriter writer, uint bytes)
        {
            // make sure we are not generating too many frames, if there
            // are other means of synchronization (e.g. waiting for the next
            // camera image from a hardware camera) this might not be necessary
            ThrottleFrameRate(time);
            // create a picture to show to the user
            graphics.Clear(Color.DarkSlateBlue);
            graphics.DrawArc(pen, circle, angle + 0, 135);
            graphics.DrawString("Hello C#\nCamera", font, Brushes.WhiteSmoke, circle, format);
            // copy the picture to our buffer, this can be done efficiently
            // since our image is already in the correct format (RGB32)
            BitmapData bmpData =
                bitmap.LockBits(rect, System.Drawing.Imaging.ImageLockMode.ReadWrite,
                bitmap.PixelFormat);
            IntPtr ptr = bmpData.Scan0;
            Marshal.Copy(ptr, this.bytes, 0, (int)bytes);
            bitmap.UnlockBits(bmpData);
            // send the picture to the media foundation frame server
            writer.Write(this.bytes);
            // make something move, so it is less boring
            angle += 12;
        }
    }

}
