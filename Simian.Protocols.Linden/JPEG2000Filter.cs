/*
 * Copyright (c) Open Metaverse Foundation
 * All rights reserved.
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions
 * are met:
 * 1. Redistributions of source code must retain the above copyright
 *    notice, this list of conditions and the following disclaimer.
 * 2. Redistributions in binary form must reproduce the above copyright
 *    notice, this list of conditions and the following disclaimer in the
 *    documentation and/or other materials provided with the distribution.
 * 3. The name of the author may not be used to endorse or promote products
 *    derived from this software without specific prior written permission.
 * 
 * THIS SOFTWARE IS PROVIDED BY THE AUTHOR ``AS IS'' AND ANY EXPRESS OR
 * IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES
 * OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED.
 * IN NO EVENT SHALL THE AUTHOR BE LIABLE FOR ANY DIRECT, INDIRECT,
 * INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT
 * NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE,
 * DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY
 * THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF
 * THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text;
using CSJ2K;
using log4net;
using OpenMetaverse;
using OpenMetaverse.Imaging;

namespace Simian.Protocols.Linden
{
    [ApplicationModule("JPEG2000Filter")]
    public class JPEG2000Filter : IApplicationModule
    {
        private static readonly ILog m_log = LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType.Name);

        private Simian m_simian;

        public bool Start(Simian simian)
        {
            m_simian = simian;
            simian.RegisterAssetFilter("image/x-j2c", JPEG2000Handler);
            return true;
        }

        public void Stop()
        {
            m_simian.UnregisterAssetFilter("image/x-j2c");
        }

        private bool JPEG2000Handler(Asset asset)
        {
            string layerBoundariesHeader = GetLayerBoundariesHeader(asset.ID, asset.Data);
            if (layerBoundariesHeader == null)
            {
                m_log.Error("Rejecting invalid JPEG2000 texture asset, ID=" + asset.ID + ", CreatorID=" + asset.CreatorID);
                return false;
            }

            if (asset.ExtraHeaders == null)
                asset.ExtraHeaders = new Dictionary<string, string>();
            asset.ExtraHeaders["X-JPEG2000-Layers"] = layerBoundariesHeader;

            int width, height;
            Color4 color = GetAverageColor(asset.ID, asset.Data, out width, out height);
            if (width != 0 && height != 0)
            {
                asset.ExtraHeaders["X-JPEG2000-RGBA"] = String.Format("{0},{1},{2},{3}", color.R, color.G, color.B, color.A);
                asset.ExtraHeaders["X-JPEG2000-Width"] = width.ToString();
                asset.ExtraHeaders["X-JPEG2000-Height"] = height.ToString();
            }

            return true;
        }

        private static string GetLayerBoundariesHeader(UUID textureID, byte[] textureData)
        {
            OpenJPEG.J2KLayerInfo[] layers = null;

            // Decode this texture and get layer boundaries before storing it
            using (MemoryStream stream = new MemoryStream(textureData))
            {
                try
                {
                    List<int> layerStarts = J2kImage.GetLayerBoundaries(stream);

                    if (layerStarts != null && layerStarts.Count > 0)
                    {
                        layers = new OpenJPEG.J2KLayerInfo[layerStarts.Count];

                        for (int i = 0; i < layerStarts.Count; i++)
                        {
                            OpenJPEG.J2KLayerInfo layer = new OpenJPEG.J2KLayerInfo();

                            if (i == 0)
                                layer.Start = 0;
                            else
                                layer.Start = layerStarts[i];

                            if (i == layerStarts.Count - 1)
                                layer.End = textureData.Length;
                            else
                                layer.End = layerStarts[i + 1] - 1;

                            layers[i] = layer;
                        }
                    }
                }
                catch (Exception ex)
                {
                    m_log.WarnFormat("Error decoding layer boundaries from texture {0} ({1} bytes): {2}", textureID, textureData.Length, ex.Message);
                    layers = null;
                }
            }

            if (layers != null)
            {
                StringBuilder header = new StringBuilder();

                for (int i = 0; i < layers.Length; i++)
                {
                    OpenJPEG.J2KLayerInfo layer = layers[i];
                    header.AppendFormat("{0}-{1};", layer.Start, layer.End);
                }

                return header.ToString();
            }
            else
            {
                return null;
            }
        }

        public static Color4 GetAverageColor(UUID textureID, byte[] textureData, out int width, out int height)
        {
            ulong r = 0;
            ulong g = 0;
            ulong b = 0;
            ulong a = 0;

            using (MemoryStream stream = new MemoryStream(textureData))
            {
                try
                {
                    Bitmap bitmap = (Bitmap)J2kImage.FromStream(stream);
                    width = bitmap.Width;
                    height = bitmap.Height;

                    BitmapData bitmapData = bitmap.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, bitmap.PixelFormat);
                    int pixelBytes = (bitmap.PixelFormat == PixelFormat.Format24bppRgb) ? 3 : 4;

                    // Sum up the individual channels
                    unsafe
                    {
                        if (pixelBytes == 4)
                        {
                            for (int y = 0; y < height; y++)
                            {
                                byte* row = (byte*)bitmapData.Scan0 + (y * bitmapData.Stride);

                                for (int x = 0; x < width; x++)
                                {
                                    b += row[x * pixelBytes + 0];
                                    g += row[x * pixelBytes + 1];
                                    r += row[x * pixelBytes + 2];
                                    a += row[x * pixelBytes + 3];
                                }
                            }
                        }
                        else
                        {
                            for (int y = 0; y < height; y++)
                            {
                                byte* row = (byte*)bitmapData.Scan0 + (y * bitmapData.Stride);

                                for (int x = 0; x < width; x++)
                                {
                                    b += row[x * pixelBytes + 0];
                                    g += row[x * pixelBytes + 1];
                                    r += row[x * pixelBytes + 2];
                                }
                            }
                        }
                    }

                    // Get the averages for each channel
                    const decimal OO_255 = 1m / 255m;
                    decimal totalPixels = (decimal)(width * height);

                    decimal rm = ((decimal)r / totalPixels) * OO_255;
                    decimal gm = ((decimal)g / totalPixels) * OO_255;
                    decimal bm = ((decimal)b / totalPixels) * OO_255;
                    decimal am = ((decimal)a / totalPixels) * OO_255;

                    if (pixelBytes == 3)
                        am = 1m;

                    return new Color4((float)rm, (float)gm, (float)bm, (float)am);
                }
                catch (Exception ex)
                {
                    m_log.WarnFormat("Error decoding texture {0} ({1} bytes): {2}", textureID, textureData.Length, ex.Message);
                    width = 0;
                    height = 0;
                    return Color4.Black;
                }
            }
        }
    }
}
