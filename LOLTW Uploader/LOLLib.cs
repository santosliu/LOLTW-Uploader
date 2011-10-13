using System;
using System.Collections.Generic;
using System.IO;
//using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Net;
using System.Net.NetworkInformation;
using System.Management;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.AccessControl;
using System.Collections.Specialized;
using Microsoft.Win32;
using Ionic.Zip;

namespace LOLTW_Uploader
{
    partial class LOLLib
    {
        		#region 常數

		private const string regLOLTW64 = @"SOFTWARE\Wow6432Node\Garena\LoLTW";
		private const string regLOLTW = @"SOFTWARE\Garena\LoLTW";
        private const string regAutoRun = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string regAutoRunKey = @"LOLTW.TK Uploader";
		private static string pathLOLTW = null;

		//private const string urlUpload = "http://chrisliu.net/test/tk.php";
        private const string urlUpload = @"http://loltw.net/upload/client_upload/";
		//private const string urlUpload = @"http://loltw.net:8000/upload/client_upload/";

        public static DateTime lastUploaded;
        private static string macAddress = "";

		private static WebClient wc = new WebClient();
		//private static FileSystemWatcher fsw;

        private static string iniPath = Environment.CurrentDirectory + @"\tklol.ini";
        private static string appPath = System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName;

		#endregion

        #region handel

        public static int HandelFiles(List<FileInfo> fileWaitUpload)
        {
            List<string> files = new List<string>();
            DateTime d = new DateTime();
            foreach (FileInfo fi in fileWaitUpload)
            {
                files.Add(fi.FullName);
                ParseLOLTime(fi.Name, out d);
            }

            string cmpName = CompressFile(files.ToArray(), string.Format("loluploader_temp.zip"));

            if (UploadFile(cmpName) == 1)
            {
                IniWriteValue("upload", "last_upload", string.Format("{0:s}", d));
                if (System.IO.File.Exists(cmpName))
                {
                    try
                    {
                        System.IO.File.Delete(cmpName);
                    }
                    catch (System.IO.IOException e)
                    {
                        return -1;
                    }
                }

                return 1;
            }

            return 0;
        }

        public static int HandelFile(FileInfo fi)
        {
            // 壓縮 log 檔存到 temp 資料夾，並重新用 key 命名
            string cmpName = CompressFile(fi.FullName, fi.Name);
            //string cmpName = path;
            DateTime d;
            if (UploadFile(cmpName) == 1)
            {
                ParseLOLTime(fi.Name, out d);
                IniWriteValue("upload", "last_upload", string.Format("{0:s}", d));
                if (System.IO.File.Exists(cmpName))
                {
                    try
                    {
                        System.IO.File.Delete(cmpName);
                    }
                    catch (System.IO.IOException e)
                    {
                        return -1;
                    }
                }

                return 1;
            }

            return 0;
        }

        public static List<FileInfo> ScanFolder(string dir)
        {
            List<FileInfo> fileWaitUpload = new List<FileInfo>();
            DateTime t;
            foreach (var item in GetFiles(dir))
            {
                if (IsFileOpenByOtherProcess(item.FullName)) continue;

                if (ParseLOLTime(item.Name, out t))
                {
                    if (t > lastUploaded)
                    fileWaitUpload.Add(item);
                }
            }
            return fileWaitUpload;
        }

        [DllImport("kernel32.dll")]
        private static extern IntPtr _lopen(string lpPathName, int iReadWrite);

        [DllImport("kernel32.dll")]
        private static extern bool CloseHandle(IntPtr hObject);

        private const int OF_READWRITE = 2;
        private const int OF_SHARE_DENY_NONE = 0x40;
        private static IntPtr HFILE_ERROR = new IntPtr(-1);

        private static bool IsFileOpenByOtherProcess(string fileName)
        {
            try
            {
                File.OpenRead(fileName);
                /*
                IntPtr vHandle = _lopen(fileName, OF_READWRITE | OF_SHARE_DENY_NONE);
                if (vHandle == HFILE_ERROR)
                {
                    return true;
                }
                

                CloseHandle(vHandle);
                */
                return false;
            }
            catch
            {
                return true;
            }
        }

        private static IEnumerable<FileInfo> GetFiles(string dir)
        {
            var dinfo = new DirectoryInfo(dir);
            foreach (var f in dinfo.GetFiles("*.log"))
                yield return f;
        }

        private static bool ParseLOLTime(string filename, out DateTime datetime) {
            //LolClient.20110927.110148.log
            //          日期.時間
            string pattern = @"^LolClient.(\d{4})(\d{2})(\d{2}).(\d{2})(\d{2})(\d{2}).log";
            Regex reg = new Regex(pattern);
            Match m = reg.Match(filename);
            return DateTime.TryParse(string.Format(@"{0}-{1}-{2} {3}:{4}:{5}", m.Groups[1].Value, m.Groups[2].Value, m.Groups[3].Value, m.Groups[4].Value, m.Groups[5].Value, m.Groups[6].Value), out datetime);
        }

        #endregion

        #region hash

        // mac address + key

		private static string CreateMD5Hash(string input)
		{
			// Use input string to calculate MD5 hash
			MD5 md5 = System.Security.Cryptography.MD5.Create();
			byte[] inputBytes = System.Text.Encoding.ASCII.GetBytes(input);
			byte[] hashBytes = md5.ComputeHash(inputBytes);

			// Convert the byte array to hexadecimal string
			StringBuilder sb = new StringBuilder();
			for (int i = 0; i < hashBytes.Length; i++)
			{
				//sb.Append(hashBytes[i].ToString("X2"));
				// To force the hex string to lower-case letters instead of
				// upper-case, use he following line instead:
				sb.Append(hashBytes[i].ToString("x2")); 
			}
			return sb.ToString();
		}

		private static string GetHashedKey()
		{
            return string.Format("{0}#{1}", CreateMD5Hash(GetMACAddress() + seed), GetMACAddress());
		}

		#endregion

		#region LOG

		public static void logit(string msg)
		{
			using (StreamWriter w = File.AppendText("log.txt"))
			{
				w.WriteLine("{0} {1}: {2}", DateTime.Now.ToLongTimeString(), DateTime.Now.ToLongDateString(), msg);
				w.Close();
			}
		}

		#endregion

		#region 壓縮

        public static string CompressFile(string path, string destFilename)
        {
            string destPath = string.Format(@"{0}\{1}.gz", Environment.GetEnvironmentVariable("TEMP"), destFilename);

            using (ZipFile zip = new ZipFile())
            {
                zip.AddFile(path);
                zip.Save(destPath);
            }

            return destPath;
        }

        public static string CompressFile(string[] path, string destFilename)
        {
            string destPath = string.Format(@"{0}\{1}.gz", Environment.GetEnvironmentVariable("TEMP"), destFilename);

            using (ZipFile zip = new ZipFile())
            {
                zip.AddFiles(path);
                zip.Save(destPath);
            }

            return destPath;
        }

		// 壓縮檔案:.net 沒提供直接壓成 zip 的 lib
		// 所以改用 gzip，server 端是 unix 的話很好處理
        /*
		public static string CompressFile(string path, string destFilename)
		{
            string destPath = string.Format(@"{0}\{1}.gz", Environment.GetEnvironmentVariable("TEMP"), destFilename);

			FileStream sourceFile = File.OpenRead(path);
            FileStream destinationFile = File.Create(destPath);

			byte[] buffer = new byte[sourceFile.Length];
			sourceFile.Read(buffer, 0, buffer.Length);

			using (GZipStream output = new GZipStream(destinationFile,
				CompressionMode.Compress))
			{
				output.Write(buffer, 0, buffer.Length);
			}

			// Close the files.
			sourceFile.Close();
			destinationFile.Close();

            return destPath;
		}
        */



		#endregion

		#region 取得regedit資訊

		/// <summary>
		/// 取得 LOL 安裝路徑
		/// </summary>
		public static string GetLOLTWPath()
		{
			if (pathLOLTW != null)
				return pathLOLTW;

			try
			{
				RegistryKey protocolKey = Registry.LocalMachine.OpenSubKey(regLOLTW64);
				if (protocolKey == null)
				{
					protocolKey = Registry.LocalMachine.OpenSubKey(regLOLTW);
					if (protocolKey != null)
					{
						pathLOLTW = protocolKey.GetValue("Path", null).ToString();
					}
				}
				else
				{
					pathLOLTW = protocolKey.GetValue("Path", null).ToString();
				}
			}
			catch (Exception)
			{
				//do nothing just return null
			}

			if (!pathLOLTW.Contains("GameData"))
			{
				pathLOLTW = pathLOLTW.Replace(@"Apps", @"GameData\Apps");
			}

			return pathLOLTW;
		}

        public static string GetLOLAutoRun()
        {
            return Registry.CurrentUser.OpenSubKey(regAutoRun).GetValue(regAutoRunKey, false).ToString();
        }

        public static void CheckLOLAutoRun(bool autorun)
        {
            try
            {
                if (autorun)
                {
                    createLOLAutoRun();
                }
                else
                {
                    removeLOLAutoRun();

                }
            }
            catch (Exception)
            {

            }
        }

        private static void CheckExePath(RegistryKey protocolKey)
        {
            createLOLAutoRun();
        }

        /// <summary>
        /// 註冊 play 的通訊協定
        /// </summary>
        private static void createLOLAutoRun()
        {
            RegistryKey protocolKey = Registry.CurrentUser.CreateSubKey(regAutoRun);
            protocolKey.SetValue(regAutoRunKey, (string)appPath, RegistryValueKind.String);
            protocolKey.Close();
            
        }

        /// <summary>
        /// 取消 play 的通訊協定
        /// </summary>
        private static void removeLOLAutoRun()
        {
            RegistryKey protocolKey = Registry.CurrentUser.CreateSubKey(regAutoRun);
            protocolKey.DeleteValue("LOLTW.TK Uploader");
            protocolKey.Close();
        }

		#endregion

		#region Hardware Inforamtion
        /*
		public static string GetMACAddress()
		{
			string macAddress = "";
			ManagementClass mc = new ManagementClass("Win32_NetworkAdapterConfiguration");
			ManagementObjectCollection moc = mc.GetInstances();
			foreach (ManagementObject mo in moc)
			{
				if ((bool)mo["IPEnabled"] == true)
					macAddress = mo["MacAddress"].ToString();
				mo.Dispose();
			}
			return macAddress;
		}
        */

        private static string GetMACAddress()
        {
            if (macAddress.Length <= 0)
            {
                string ma = null;
                NetworkInterface[] nics = NetworkInterface.GetAllNetworkInterfaces();
                foreach (NetworkInterface adapter in nics)
                {
                    if (adapter.GetPhysicalAddress().ToString().Length > 0)
                    {
                        // 這個取得的沒有 - 
                        //macAddress = adapter.GetPhysicalAddress().ToString();

                        PhysicalAddress address = adapter.GetPhysicalAddress();
                        byte[] bytes = address.GetAddressBytes();
                        for (int i = 0; i < bytes.Length; i++)
                        {
                            ma += string.Format("{0}", bytes[i].ToString("X2"));
                            if (i != bytes.Length - 1)
                            {
                                ma += "-";
                            }
                        }

                        macAddress = ma;

                        return macAddress;

                    }
                }
            }
            else
            {
                return macAddress;
            }

            return null;
        }


		#endregion

		#region Network

		public static int UploadFile(string path)
		{
			wc = new WebClient();
			//byte[] responseArray = wc.UploadFile(urlUpload, "POST", path);
            //string tet = System.Text.Encoding.ASCII.GetString(responseArray);
            string result = UploadFileEx(path, urlUpload, GetHashedKey(), null, null);
            //return int.Parse(result);
            return 1;
		}

        public static string UploadFileEx(string uploadfile, string url, string fileFormName, string contenttype, NameValueCollection querystring)
        {
            System.Net.ServicePointManager.Expect100Continue = false;

            if ((fileFormName == null) ||
                (fileFormName.Length == 0))
            {
                fileFormName = "file";
            }

            if ((contenttype == null) ||
                (contenttype.Length == 0))
            {
                contenttype = "application/octet-stream";
            }


            string postdata;
            postdata = "?";
            if (querystring != null)
            {
                foreach (string key in querystring.Keys)
                {
                    postdata += key + "=" + querystring.Get(key) + "&";
                }
            }
            Uri uri = new Uri(url + postdata);


            string boundary = "----------" + DateTime.Now.Ticks.ToString("x");
            HttpWebRequest webrequest = (HttpWebRequest)WebRequest.Create(uri);
            webrequest.ContentType = "multipart/form-data; boundary=" + boundary;
            webrequest.Method = "POST";


            // Build up the post message header

            StringBuilder sb = new StringBuilder();
            sb.Append("--");
            sb.Append(boundary);
            sb.Append("\r\n");
            sb.Append("Content-Disposition: form-data; name=\"");
            sb.Append(fileFormName);
            sb.Append("\"; filename=\"");
            sb.Append(Path.GetFileName(uploadfile));
            sb.Append("\"");
            sb.Append("\r\n");
            sb.Append("Content-Type: ");
            sb.Append(contenttype);
            sb.Append("\r\n");
            sb.Append("\r\n");

            string postHeader = sb.ToString();
            byte[] postHeaderBytes = Encoding.UTF8.GetBytes(postHeader);

            // Build the trailing boundary string as a byte array

            // ensuring the boundary appears on a line by itself

            byte[] boundaryBytes =
                   Encoding.ASCII.GetBytes("\r\n--" + boundary + "\r\n");

            FileStream fileStream = new FileStream(uploadfile,
                                        FileMode.Open, FileAccess.Read);
            long length = postHeaderBytes.Length + fileStream.Length +
                                                   boundaryBytes.Length;
            webrequest.ContentLength = length;

            Stream requestStream = webrequest.GetRequestStream();

            // Write out our post header

            requestStream.Write(postHeaderBytes, 0, postHeaderBytes.Length);

            // Write out the file contents

            byte[] buffer = new Byte[checked((uint)Math.Min(4096,
                                     (int)fileStream.Length))];
            int bytesRead = 0;
            while ((bytesRead = fileStream.Read(buffer, 0, buffer.Length)) != 0)
                requestStream.Write(buffer, 0, bytesRead);

            // Write out the trailing boundary

            requestStream.Write(boundaryBytes, 0, boundaryBytes.Length);
            WebResponse responce = webrequest.GetResponse();
            Stream s = responce.GetResponseStream();
            StreamReader sr = new StreamReader(s);

            return sr.ReadToEnd();
        }

		#endregion

		#region ini 設定檔

		[DllImport("kernel32")]
		private static extern long WritePrivateProfileString(string section, string key, string val, string filePath);
		[DllImport("kernel32")]
		private static extern int GetPrivateProfileString(string section,  string key, string def, StringBuilder retVal, int size, string filePath);

		/// <summary>
		/// Write Data to the INI File
		/// </summary>
		/// <PARAM name="Section"></PARAM>
		/// Section name
		/// <PARAM name="Key"></PARAM>
		/// Key Name
		/// <PARAM name="Value"></PARAM>
		/// Value Name

		public static void IniWriteValue(string Section, string Key, string Value)
		{
			WritePrivateProfileString(Section, Key, Value, iniPath);
		}

		/// <summary>
		/// Read Data Value From the Ini File
		/// </summary>
		/// <PARAM name="Section"></PARAM>
		/// <PARAM name="Key"></PARAM>
		/// <PARAM name="Path"></PARAM>
		/// <returns></returns>
		public static string IniReadValue(string Section, string Key)
		{
			StringBuilder temp = new StringBuilder(255);
			int i = GetPrivateProfileString(Section, Key, "", temp, 255, iniPath);
			return temp.ToString();

		}

		#endregion
    }
}
