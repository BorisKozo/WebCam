using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using AviFile;


namespace WebCam
{
  public partial class MainForm : Form
  {
    class CaptureObject
    {
      private string _targetPath;
      private List<Bitmap> _images;
      private int _counter = 0;
      private int _fps = 15;

      public event EventHandler CaptureFinished;

      public CaptureObject(string path)
      {
        _targetPath = Path.Combine(path, DateTime.Now.ToString("yyyyMMdd_HHmmss_F"));
        Directory.CreateDirectory(_targetPath);
      }


      public void Start(int futureImagesCount, IEnumerable<Bitmap> previousImages, int fps)
      {
        _images = new List<Bitmap>();
        foreach (Bitmap bitmap in previousImages)
        {
          _images.Add(bitmap);
        }

        _counter = futureImagesCount;
        _fps = fps;
      }

      public void Capture(Bitmap image)
      {
        if (Counter > 0)
        {
          _images.Add(image);
          _counter--;
          if (Counter == 0 && CaptureFinished != null)
          {
            CaptureFinished(this, new EventArgs());
          }
        }
      }

      public void Save()
      {
        int counter = 0;
        foreach (Bitmap image in _images)
        {
          image.Save(Path.Combine(_targetPath, "image" + counter.ToString() + ".png"), ImageFormat.Png);
          counter++;
        }
        ClearImages();
      }

      public void SaveAsVideo()
      {

        Bitmap bmp = _images[0];
        AviManager aviManager = new AviManager(Path.Combine(_targetPath, "video.avi"), false);
        VideoStream aviStream = aviManager.AddVideoStream(false, _fps, bmp);
        for (int n = 1; n < _images.Count; n++)
        {
          aviStream.AddFrame(_images[n]);
        }

        ClearImages();
        aviManager.Close();
      }

      private void ClearImages()
      {
        foreach (Bitmap bitmap in _images)
          bitmap.Dispose();
        _images.Clear();
      }


      public int Counter
      {
        get { return _counter; }
      }
    }

    enum HotSpotMode { None, Start, End };

    private DateTime _lastTime = DateTime.Now;
    private Queue<Bitmap> _images = new Queue<Bitmap>();
    private int _maxBuffer = 30;
    private string _dataPath;
    private CaptureObject _capturer = null;
    private int _fps;


    private HotSpotMode _hotSpotMode = HotSpotMode.None;
    private Point _hotSpotStartPoint;
    private List<HotSpot> _hotSpots = new List<HotSpot>();

    const int VIDEODEVICE = 0; // zero based index of video capture device to use
    const int VIDEOWIDTH = 640; // Depends on video device caps
    const int VIDEOHEIGHT = 480; // Depends on video device caps
    const int VIDEOBITSPERPIXEL = 24; // BitsPerPixel values determined by device
    private Capture _camera;
    private IntPtr _ip;

    private void CaptureVideo()
    {
      if (_capturer != null)
      {
        return;
      }

      _capturer = new CaptureObject(_dataPath);
      _capturer.Start(_maxBuffer * 2, _images, _fps);
      _capturer.CaptureFinished += _capturer_CaptureFinished;
      toolStripStatusLabel1.Text = "Capturing";
    }

    void _capturer_CaptureFinished(object sender, EventArgs e)
    {
      _capturer.SaveAsVideo();
      _capturer = null;
      _images.Clear();
      toolStripStatusLabel1.Text = "Done";
    }

    public MainForm()
    {
      InitializeComponent();

      timer1.Tick += timer1_Tick;
      _camera = new Capture(VIDEODEVICE, VIDEOWIDTH, VIDEOHEIGHT, VIDEOBITSPERPIXEL, pictureBox2);

      string directoryName = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
      _dataPath = Path.Combine(directoryName, "Data");

      if (!Directory.Exists(_dataPath))
      {
        Directory.CreateDirectory(_dataPath);
      }

      label2.Text = "Sensitivity: " + trackBar1.Value;

      var hotSpotsFileName = Path.Combine(directoryName, "hotspots.txt");
      if (File.Exists(hotSpotsFileName))
      {
        using (StreamReader reader = new StreamReader(hotSpotsFileName))
        {
          while (!reader.EndOfStream)
          {
            string line = reader.ReadLine();
            string[] data = line.Split(',');
            Rectangle rect = new Rectangle(Convert.ToInt32(data[0]), Convert.ToInt32(data[1]), Convert.ToInt32(data[2]), Convert.ToInt32(data[3]));
            HotSpot hotSpot = new HotSpot(rect);
            _hotSpots.Add(hotSpot);
            listView1.Items.Add(hotSpot.ToString());

          }
        }
      }
    }

    void timer1_Tick(object sender, EventArgs e)
    {
      DateTime time = DateTime.Now;
      TimeSpan span = time - _lastTime;
      _lastTime = time;
      _fps = Convert.ToInt32(Math.Round(1000.0 / Math.Max(span.TotalMilliseconds, 1)));
      label1.Text = "FPS: " + _fps.ToString();
      Bitmap bitmap = GenerateImage();
      Bitmap prevImage = null;
      if (_capturer != null)
      {
        _capturer.Capture(bitmap);
      }
      else
      {
        if (_images.Count > 0)
          prevImage = _images.Last();
        pictureBox1.Image = bitmap;
        _images.Enqueue(bitmap);
        DrawHotSpots(bitmap);
        if (_images.Count > _maxBuffer)
          _images.Dequeue().Dispose();

        if (_images.Count == _maxBuffer && _hotSpots.Count > 0)
        {
          bool checkChanges = CheckChanges(bitmap, prevImage, _hotSpots, trackBar1.Value);
          if (checkChanges)
          {
            CaptureVideo();
            toolStripStatusLabel2.Text = checkChanges.ToString();
          }
        }
      }

    }

    private void button1_Click(object sender, EventArgs e)
    {
      timer1.Interval = 200;
      timer1.Start();

    }

    private Bitmap GenerateImage()
    {
      IntPtr ip;
      // capture image
      ip = _camera.Click();
      Bitmap b = new Bitmap(_camera.Width, _camera.Height, _camera.Stride, PixelFormat.Format24bppRgb, ip);

      // If the image is upsidedown
      b.RotateFlip(RotateFlipType.RotateNoneFlipY);


      if (ip != IntPtr.Zero)
      {
        Marshal.FreeCoTaskMem(ip);
        ip = IntPtr.Zero;
      }

      GC.Collect();
      return b;
    }


    private bool CheckChanges(Bitmap image, Bitmap prevImage, List<HotSpot> hotSpots, int sensitivity)
    {

      foreach (HotSpot hotSpot in hotSpots)
      {
        float sum = 0;
        float result = 0;
        float leftPixel;
        float rightPixel;
        for (int i = hotSpot.Bound.Left; i < hotSpot.Bound.Right; i++)
        {
          for (int j = hotSpot.Bound.Top; j < hotSpot.Bound.Bottom; j++)
          {
            leftPixel = RgbToGray(image.GetPixel(i, j));
            rightPixel = RgbToGray(prevImage.GetPixel(i, j));
            result += Math.Abs(leftPixel - rightPixel);
            sum += 1;
          }
        }

        result = result / sum;
        Console.WriteLine(result);
        if (result > sensitivity)
        {
          return true;
        }
      }

      return false;

    }

    private float RgbToGray(Color color)
    {
      return 0.299f * Convert.ToSingle(color.R) + 0.587f * Convert.ToSingle(color.G) + 0.114f * Convert.ToSingle(color.B);
    }

    private void DrawHotSpots(Bitmap bitmap)
    {
      Graphics g = Graphics.FromImage(bitmap);
      Pen pen = new Pen(Color.Blue);
      foreach (HotSpot hotSpot in _hotSpots)
      {
        g.DrawRectangle(pen, hotSpot.Bound);
      }
    }

    private void button2_Click(object sender, EventArgs e)
    {
      if (_capturer != null)
      {
        return;
      }
      CaptureVideo();
    }

    private void button3_Click(object sender, EventArgs e)
    {
      _hotSpotMode = HotSpotMode.Start;
    }

    private void pictureBox1_MouseDown(object sender, MouseEventArgs e)
    {
      if (_hotSpotMode == HotSpotMode.Start)
      {
        _hotSpotStartPoint = new Point(e.X, e.Y);
        _hotSpotMode = HotSpotMode.End;
        return;
      }

      if (_hotSpotMode == HotSpotMode.End)
      {
        int x = Math.Min(_hotSpotStartPoint.X, e.X);
        int y = Math.Min(_hotSpotStartPoint.Y, e.Y);
        int width = Math.Abs(_hotSpotStartPoint.X - e.X);
        int height = Math.Abs(_hotSpotStartPoint.Y - e.Y);
        HotSpot hotSpot = new HotSpot(new Rectangle(x, y, width, height));
        _hotSpots.Add(hotSpot);
        listView1.Items.Add(hotSpot.ToString());
        _hotSpotMode = HotSpotMode.None;
        return;
      }

    }

    private void pictureBox1_MouseMove(object sender, MouseEventArgs e)
    {
      if (_hotSpotMode == HotSpotMode.End && pictureBox1.Image != null)
      {
        int x = Math.Min(_hotSpotStartPoint.X, e.X);
        int y = Math.Min(_hotSpotStartPoint.Y, e.Y);
        int width = Math.Abs(_hotSpotStartPoint.X - e.X);
        int height = Math.Abs(_hotSpotStartPoint.Y - e.Y);
        Graphics g = Graphics.FromImage(pictureBox1.Image);
        g.DrawRectangle(new Pen(Color.Red), new Rectangle(x, y, width, height));
      }
    }

    private void button4_Click(object sender, EventArgs e)
    {
      if (listView1.SelectedIndices.Count > 0)
      {
        _hotSpots.RemoveAt(listView1.SelectedIndices[0]);
        listView1.Items.RemoveAt(listView1.SelectedIndices[0]);
      }
    }

    private void trackBar1_ValueChanged(object sender, EventArgs e)
    {
      label2.Text = "Sensitivity: " + trackBar1.Value;
    }

    private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
    {
      timer1.Stop();
      _camera.Dispose();
    }
  }
}
