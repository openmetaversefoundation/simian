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
using System.IO;
using Simian;
using Tests.Simian;
using NTime.Framework;
using OpenMetaverse;

namespace Tests.Simian.Performance
{
    [TimerFixture]
    public class ImageTests
    {
        const string IMAGE_FILENAME = "plywood.j2c";

        private byte[] m_jpeg2000Data;
        private MemoryStream m_jpeg2000Stream;

        [TimerFixtureSetUp]
        public void GlobalSetUp()
        {
        }

        [TimerFixtureTearDown]
        public void GlobalTearDown()
        {
        }

        [TimerSetUp]
        public void LocalSetUp()
        {
            m_jpeg2000Data = File.ReadAllBytes(Path.Combine(GetCurrentExecutingDirectory(), IMAGE_FILENAME));
            m_jpeg2000Stream = new MemoryStream(m_jpeg2000Data);
        }

        [TimerTearDown]
        public void LocalTearDown()
        {
            m_jpeg2000Stream.Dispose();
            m_jpeg2000Data = null;
        }

        [TimerDurationTest(1, Unit = TimePeriod.Millisecond)]
        public void JPEG2000DecodeLayersTest()
        {
            List<int> boundaries = CSJ2K.J2kImage.GetLayerBoundaries(m_jpeg2000Stream);
        }

        [TimerDurationTest(200, Unit = TimePeriod.Millisecond)]
        public void JPEG2000DecodeTest()
        {
            System.Drawing.Image image = CSJ2K.J2kImage.FromStream(m_jpeg2000Stream);
        }

        [TimerDurationTest(200, Unit = TimePeriod.Millisecond)]
        public void JPEG2000AverageColorTest()
        {
            int width, height;
            Color4 avgColor = global::Simian.Protocols.Linden.JPEG2000Filter.GetAverageColor(UUID.Zero, m_jpeg2000Data, out width, out height);
        }

        private static string GetCurrentExecutingDirectory()
        {
            string filePath = new Uri(System.Reflection.Assembly.GetExecutingAssembly().CodeBase).LocalPath;
            return Path.GetDirectoryName(filePath);
        }
    }
}
