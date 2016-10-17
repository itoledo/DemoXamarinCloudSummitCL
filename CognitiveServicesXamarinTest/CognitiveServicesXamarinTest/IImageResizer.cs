using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CognitiveServicesXamarinTest
{
    public interface IImageResizer
    {
        byte[] ResizeImage(byte[] imageData, float width, float height);
    }
}
