namespace DesktopUp.Application;

public sealed record ContentDragBitmap(byte[] Pixels, int Width, int Height);
public static class ContentDragPixels
{
    // Cached premultiplied BGRA only; no Shell calls or image decoding on pointer motion.
    public static ContentDragBitmap Build(ReadOnlySpan<byte> pixels, int width, int height, int size, int count)
    {
        size=Math.Clamp(size,32,160);count=Math.Clamp(count,1,100);
        int extent=size+(count>1?12:0);
        var result=new byte[extent*extent*4];
        bool valid=width>0&&height>0&&width<=1024&&height<=1024&&pixels.Length==width*height*4;
        int w=valid?Math.Max(1,(int)(size*Math.Min(1d,(double)width/height))):size;
        int h=valid?Math.Max(1,(int)(size*Math.Min(1d,(double)height/width))):size;
        void Pixel(int x,int y,byte b,byte g,byte r,byte a)
        {int p=(y*extent+x)*4;result[p]=b;result[p+1]=g;result[p+2]=r;result[p+3]=a;}
        if(count>1) for(int y=12;y<extent;y++)for(int x=12;x<extent;x++)
            if(x<18||y<18||x>=extent-2||y>=extent-2)Pixel(x,y,130,100,70,170);
        for(int y=0;y<h;y++)for(int x=0;x<w;x++)
        {
            int tx=x+(size-w)/2,ty=y+(size-h)/2;
            if(valid){int p=((y*height/h)*width+x*width/w)*4;Pixel(tx,ty,pixels[p],pixels[p+1],pixels[p+2],pixels[p+3]);}
            else if(x>size/5&&x<size*4/5&&y>size/9&&y<size*8/9)
                Pixel(tx,ty,220,220,220,255); // recognizable document, not an empty box
        }
        if(count>1)
        {
            string text=count.ToString(System.Globalization.CultureInfo.InvariantCulture);
            int bw=text.Length*8+6,bx=extent-bw,by=extent-18;
            for(int y=by;y<extent;y++)for(int x=bx;x<extent;x++)Pixel(x,y,180,95,35,255);
            string[] glyphs=["111101101101111","010110010010111","111001111100111","111001111001111","101101111001001","111100111001111","111100111101111","111001001001001","111101111101111","111101111001111"];
            for(int i=0;i<text.Length;i++)for(int y=0;y<5;y++)for(int x=0;x<3;x++)
                if(glyphs[text[i]-'0'][y*3+x]=='1')for(int dy=0;dy<2;dy++)for(int dx=0;dx<2;dx++)
                    Pixel(bx+3+i*8+x*2+dx,by+4+y*2+dy,255,255,255,255);
        }
        return new(result,extent,extent);
    }
}
