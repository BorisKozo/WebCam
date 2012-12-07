using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WebCam
{
  public class HotSpot
  {
    private Rectangle _bound;

    public HotSpot(Rectangle bound)
    {
      _bound = bound;
    }

    public override string ToString()
    {
      return string.Format("({0},{1},{2},{3})", Bound.Left, Bound.Top, Bound.Width, Bound.Height);
    }

    public Rectangle Bound
    {
      get { return _bound; }
    }




  }
}
