using System;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace WtSkin
{
    /// <summary>一次签名导入 / 移除的结果：Message 面向用户，Detail 原样写安装日志。</summary>
    internal class CertResult
    {
        public bool Ok;
        public bool AlreadyInstalled;
        public string Message = "";
        public string Detail = "";
        public string Fingerprint = PublisherCert.ExpectedThumbprint;
        public string Source = "";

        public static CertResult Fail(string message, string detail)
        {
            CertResult result = new CertResult();
            result.Ok = false;
            result.Message = message;
            result.Detail = detail;
            return result;
        }

        public static CertResult Success(string message, string detail)
        {
            CertResult result = new CertResult();
            result.Ok = true;
            result.Message = message;
            result.Detail = detail;
            return result;
        }
    }

    /// <summary>
    /// 发布者签名（代码签名证书，仅公钥）。证书随包分发：优先读 exe 旁边
    /// assets\certs\publisher.cer，没有则读内嵌资源 publisher.cer；
    /// 导入前按内置 SHA-256 校验，导入到 CurrentUser\TrustedPublisher（不需要管理员权限）。
    /// 私钥（.pfx）永远不随包，也不在这里读取。
    /// </summary>
    internal static class PublisherCert
    {
        public const string PublisherName = "Nchoneko";
        public const string ExpectedSubject = "CN=Nchoneko, E=nchoneko, C=CN, S=Guangdong";
        public const string ExpectedThumbprint = "9F5C55010135AC414C2B9CA677550AB968D71D76";
        public const string ExpectedSha256 = "1DBFA4F88846892697ED714F02E562F86D0FA01A1105FA1E8B8C73F9D2643DF2";
        public const string ResourceName = "publisher.cer";

        /// <summary>自测可改的根目录；正式运行取 exe 所在目录。</summary>
        public static string BaseDir = "";

        /// <summary>本次运行中用户已选过「暂不安装」，安装向导不再重复询问。</summary>
        public static bool SkippedThisRun;

        /// <summary>自测注入点：返回要使用的证书存储；正式运行始终是 CurrentUser 受信任发布者。</summary>
        internal static Func<X509Store> StoreOpener;

        public static string RootDir
        {
            get
            {
                string dir = string.IsNullOrEmpty(BaseDir)
                    ? AppDomain.CurrentDomain.BaseDirectory
                    : BaseDir;
                return dir.TrimEnd('\\');
            }
        }

        public static string PackagePath
        {
            get { return Path.Combine(RootDir, "assets", "certs", "publisher.cer"); }
        }

        public static string Sha256Hex(byte[] data)
        {
            if (data == null)
            {
                return "";
            }
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(data);
                StringBuilder sb = new StringBuilder(hash.Length * 2);
                for (int i = 0; i < hash.Length; i++)
                {
                    sb.Append(hash[i].ToString("X2"));
                }
                return sb.ToString();
            }
        }

        /// <summary>按指纹查当前用户的受信任发布者列表。</summary>
        public static bool IsInstalled()
        {
            X509Certificate2 found;
            return TryFindInstalled(out found);
        }

        public static bool TryFindInstalled(out X509Certificate2 found)
        {
            found = null;
            X509Store store = null;
            try
            {
                store = OpenStore();
                store.Open(OpenFlags.ReadOnly);
                found = FindInStore(store);
                return found != null;
            }
            catch
            {
                return false;
            }
            finally
            {
                CloseStore(store);
            }
        }

        /// <summary>
        /// 读取随包证书：外部文件优先（哈希不符即拒绝，不回退到内嵌，避免悄悄换成另一份证书），
        /// 没有外部文件时读 exe 内嵌资源。bytes 为 null 表示不弹窗、跳过导入。
        /// </summary>
        public static CertResult LoadPackage(out byte[] bytes, out string source)
        {
            bytes = null;
            source = "";
            string file = PackagePath;
            if (File.Exists(file))
            {
                try
                {
                    bytes = File.ReadAllBytes(file);
                }
                catch (Exception ex)
                {
                    CertResult read = CertResult.Fail(Lang.T("读取证书文件失败，已跳过签名安装。", "Failed to read the certificate file; signature installation skipped."),
                        Lang.T("读取失败：", "Read failed: ") + file + "：" + ex.Message + Lang.T("；指纹 ", "; thumbprint ") + ExpectedThumbprint);
                    read.Source = file;
                    return read;
                }
                string actual = Sha256Hex(bytes);
                if (!string.Equals(actual, ExpectedSha256, StringComparison.OrdinalIgnoreCase))
                {
                    bytes = null;
                    CertResult bad = CertResult.Fail(Lang.T("证书文件已损坏或被修改（SHA-256 校验不通过），已拒绝导入。", "The certificate is damaged or modified (SHA-256 mismatch); import refused."),
                        Lang.T("哈希不符：期望 ", "Hash mismatch: expected ") + ExpectedSha256 + Lang.T("，实际 ", ": actual ") + actual + Lang.T("，来源 ", ", source ") + file +
                        Lang.T("；指纹 ", "; thumbprint ") + ExpectedThumbprint);
                    bad.Source = file;
                    return bad;
                }
                source = file;
                return CertResult.Success(Lang.T("证书文件校验通过。", "Certificate file verified."),
                    Lang.T("读取外部证书文件：", "External certificate read: ") + file + Lang.T("（SHA-256 校验通过，", " (SHA-256 verified, ") + ExpectedSha256 + "）");
            }

            try
            {
                Assembly assembly = typeof(PublisherCert).Assembly;
                using (Stream stream = assembly.GetManifestResourceStream(ResourceName))
                {
                    if (stream == null)
                    {
                        CertResult missing = CertResult.Fail(Lang.T("没有随包找到发布者证书，已跳过签名安装。", "No publisher certificate found; signature installation skipped."),
                            "证书缺失：exe 内嵌资源 " + ResourceName + " 不存在，外部文件 " + file +
                            Lang.T(" 也不存在；指纹 ", " also missing; thumbprint ") + ExpectedThumbprint);
                        return missing;
                    }
                    using (MemoryStream memory = new MemoryStream())
                    {
                        byte[] buffer = new byte[4096];
                        int read;
                        while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            memory.Write(buffer, 0, read);
                        }
                        bytes = memory.ToArray();
                    }
                }
            }
            catch (Exception ex)
            {
                bytes = null;
                return CertResult.Fail(Lang.T("读取内嵌证书失败，已跳过签名安装。", "Failed to read embedded certificate; signature installation skipped."),
                    Lang.T("读取内嵌资源失败：", "Failed to read embedded resource: ") + ResourceName + "：" + ex.Message);
            }

            if (bytes == null || bytes.Length == 0)
            {
                bytes = null;
                return CertResult.Fail(Lang.T("没有随包找到发布者证书，已跳过签名安装。", "No publisher certificate found; signature installation skipped."),
                    "证书缺失：内嵌资源 " + ResourceName + Lang.T(" 为空；指纹 ", " is empty; thumbprint ") + ExpectedThumbprint);
            }
            string embeddedHash = Sha256Hex(bytes);
            if (!string.Equals(embeddedHash, ExpectedSha256, StringComparison.OrdinalIgnoreCase))
            {
                bytes = null;
                return CertResult.Fail(Lang.T("内嵌证书校验不通过，已拒绝导入。", "Embedded certificate failed verification; import refused."),
                    Lang.T("内嵌证书哈希不符：期望 ", "Embedded certificate hash mismatch: expected ") + ExpectedSha256 + Lang.T("，实际 ", ": actual ") + embeddedHash);
            }
            source = "exe 内嵌资源 " + ResourceName;
            return CertResult.Success(Lang.T("内嵌证书校验通过。", "Embedded certificate verified."),
                "读取内嵌证书 " + ResourceName + Lang.T("（SHA-256 校验通过，", " (SHA-256 verified, ") + ExpectedSha256 + "）");
        }

        /// <summary>读取随包证书并导入当前用户的受信任发布者。</summary>
        public static CertResult InstallPackage()
        {
            byte[] bytes;
            string source;
            CertResult load = LoadPackage(out bytes, out source);
            if (!load.Ok || bytes == null)
            {
                return load;
            }
            return InstallBytes(bytes, true, source);
        }

        /// <summary>导入指定证书内容；verifyHash=true 时先比对内置 SHA-256。</summary>
        public static CertResult InstallBytes(byte[] bytes, bool verifyHash, string source)
        {
            X509Certificate2 cert;
            string error;
            if (!TryLoadCertificate(bytes, verifyHash, out cert, out error))
            {
                CertResult bad = CertResult.Fail(Lang.T("证书文件已损坏或不是有效的证书，已拒绝导入。", "The certificate is damaged or invalid; import refused."),
                    Lang.T("证书校验失败：", "Certificate verification failed: ") + error + Lang.T("；期望指纹 ", "; expected thumbprint ") + ExpectedThumbprint);
                bad.Source = source;
                return bad;
            }

            X509Store store = null;
            try
            {
                store = OpenStore();
                store.Open(OpenFlags.ReadWrite);
                if (FindInStore(store) != null)
                {
                    CertResult already = CertResult.Success(Lang.T("发布者签名已经安装过了。", "The publisher signature is already installed."),
                        Lang.T("已存在（TrustedPublisher / CurrentUser），未重复导入；指纹 ", "Already present (TrustedPublisher / CurrentUser); not re-imported; thumbprint ") + ExpectedThumbprint);
                    already.AlreadyInstalled = true;
                    already.Source = source;
                    return already;
                }
                store.Add(cert);
                // 导入后按指纹复查，确认真的进了列表。
                if (FindInStore(store) == null)
                {
                    CertResult lost = CertResult.Fail(Lang.T("导入后复查不到证书，可能被系统策略拦截。", "Certificate not found after import; a system policy may have blocked it."),
                        Lang.T("导入后按指纹复查失败：", "Post-import thumbprint check failed: ") + ExpectedThumbprint + Lang.T("；来源 ", "; source ") + source);
                    lost.Source = source;
                    return lost;
                }
                CertResult ok = CertResult.Success(Lang.T("发布者签名已安装（", "Publisher signature installed (") + PublisherName + "）。",
                    Lang.T("导入成功：TrustedPublisher / CurrentUser，按指纹复查通过；指纹 ", "Import succeeded: TrustedPublisher / CurrentUser, thumbprint verified; ") + ExpectedThumbprint +
                    Lang.T("；来源 ", "; source ") + source);
                ok.Source = source;
                return ok;
            }
            catch (UnauthorizedAccessException ex)
            {
                CertResult denied = CertResult.Fail(Lang.T("没有权限修改受信任发布者列表，已跳过签名安装。", "No permission to modify trusted publishers; signature installation skipped."),
                    Lang.T("导入失败（权限）：", "Import failed (permission): ") + ex.Message + Lang.T("；指纹 ", "; thumbprint ") + ExpectedThumbprint);
                denied.Source = source;
                return denied;
            }
            catch (System.Security.SecurityException ex)
            {
                CertResult denied = CertResult.Fail(Lang.T("系统拒绝了证书存储操作，已跳过签名安装。", "The system rejected the certificate store operation; skipped."),
                    Lang.T("导入失败（权限）：", "Import failed (permission): ") + ex.Message + Lang.T("；指纹 ", "; thumbprint ") + ExpectedThumbprint);
                denied.Source = source;
                return denied;
            }
            catch (CryptographicException ex)
            {
                CertResult corrupt = CertResult.Fail(Lang.T("证书损坏或被证书存储拒绝，已跳过签名安装。", "Certificate damaged or rejected by the store; skipped."),
                    Lang.T("导入失败（证书损坏 / 存储拒绝）：", "Import failed (damaged certificate / store rejection): ") + ex.Message + Lang.T("；指纹 ", "; thumbprint ") + ExpectedThumbprint);
                corrupt.Source = source;
                return corrupt;
            }
            catch (Exception ex)
            {
                CertResult failed = CertResult.Fail(Lang.T("导入失败：", "Import failed: ") + ex.Message,
                    Lang.T("导入失败：", "Import failed: ") + ex.GetType().Name + "：" + ex.Message + Lang.T("；指纹 ", "; thumbprint ") + ExpectedThumbprint);
                failed.Source = source;
                return failed;
            }
            finally
            {
                CloseStore(store);
            }
        }

        /// <summary>从当前用户的受信任发布者里移除本程序的签名（按指纹）。</summary>
        public static CertResult Remove()
        {
            X509Store store = null;
            try
            {
                store = OpenStore();
                store.Open(OpenFlags.ReadWrite);
                X509Certificate2Collection list =
                    store.Certificates.Find(X509FindType.FindByThumbprint, ExpectedThumbprint, false);
                if (list.Count == 0)
                {
                    CertResult none = CertResult.Success(Lang.T("当前用户的受信任发布者里没有这个签名。", "This signature is not in the current user's trusted publishers."),
                        Lang.T("无需移除：TrustedPublisher / CurrentUser 里没有指纹 ", "Nothing to remove: no thumbprint in TrustedPublisher / CurrentUser ") + ExpectedThumbprint);
                    none.AlreadyInstalled = false;
                    return none;
                }
                for (int i = 0; i < list.Count; i++)
                {
                    store.Remove(list[i]);
                }
                if (FindInStore(store) != null)
                {
                    return CertResult.Fail(Lang.T("移除后仍能按指纹查到证书，请稍后重试。", "Certificate still found after removal; try again later."),
                        Lang.T("移除后复查仍存在：指纹 ", "Still present after removal: thumbprint ") + ExpectedThumbprint);
                }
                CertResult ok = CertResult.Success(Lang.T("发布者签名已从当前用户移除。", "Publisher signature removed for the current user."),
                    Lang.T("移除成功：TrustedPublisher / CurrentUser，指纹 ", "Removed: TrustedPublisher / CurrentUser, thumbprint ") + ExpectedThumbprint);
                return ok;
            }
            catch (UnauthorizedAccessException ex)
            {
                return CertResult.Fail(Lang.T("没有权限修改受信任发布者列表。", "No permission to modify trusted publishers."),
                    Lang.T("移除失败（权限）：", "Removal failed (permission): ") + ex.Message + Lang.T("；指纹 ", "; thumbprint ") + ExpectedThumbprint);
            }
            catch (System.Security.SecurityException ex)
            {
                return CertResult.Fail(Lang.T("系统拒绝了证书存储操作。", "The system rejected the certificate store operation."),
                    Lang.T("移除失败（权限）：", "Removal failed (permission): ") + ex.Message + Lang.T("；指纹 ", "; thumbprint ") + ExpectedThumbprint);
            }
            catch (CryptographicException ex)
            {
                return CertResult.Fail(Lang.T("移除失败：证书存储报错。", "Removal failed: certificate store error."),
                    Lang.T("移除失败（证书存储）：", "Removal failed (certificate store): ") + ex.Message + Lang.T("；指纹 ", "; thumbprint ") + ExpectedThumbprint);
            }
            catch (Exception ex)
            {
                return CertResult.Fail(Lang.T("移除失败：", "Removal failed: ") + ex.Message,
                    Lang.T("移除失败：", "Removal failed: ") + ex.GetType().Name + "：" + ex.Message + Lang.T("；指纹 ", "; thumbprint ") + ExpectedThumbprint);
            }
            finally
            {
                CloseStore(store);
            }
        }

        public static bool TryLoadCertificate(byte[] bytes, bool verifyHash, out X509Certificate2 cert, out string error)
        {
            cert = null;
            error = "";
            if (bytes == null || bytes.Length == 0)
            {
                error = Lang.T("证书内容为空", "Certificate is empty");
                return false;
            }
            if (verifyHash)
            {
                string actual = Sha256Hex(bytes);
                if (!string.Equals(actual, ExpectedSha256, StringComparison.OrdinalIgnoreCase))
                {
                    error = Lang.T("SHA-256 不一致（期望 ", "SHA-256 mismatch (expected ") + ExpectedSha256 + Lang.T("，实际 ", ": actual ") + actual + "）";
                    return false;
                }
            }
            try
            {
                X509Certificate2 loaded = new X509Certificate2(bytes);
                if (!string.Equals(loaded.Thumbprint, ExpectedThumbprint, StringComparison.OrdinalIgnoreCase))
                {
                    error = Lang.T("指纹不一致（期望 ", "Thumbprint mismatch (expected ") + ExpectedThumbprint + Lang.T("，实际 ", ": actual ") + loaded.Thumbprint + "）";
                    return false;
                }
                cert = loaded;
                return true;
            }
            catch (CryptographicException ex)
            {
                error = Lang.T("证书内容无法解析（", "Cannot parse certificate (") + ex.Message + "）";
                return false;
            }
            catch (Exception ex)
            {
                error = Lang.T("读取证书失败（", "Failed to read certificate (") + ex.Message + "）";
                return false;
            }
        }

        private static X509Store OpenStore()
        {
            Func<X509Store> opener = StoreOpener;
            if (opener != null)
            {
                return opener();
            }
            return new X509Store(StoreName.TrustedPublisher, StoreLocation.CurrentUser);
        }

        private static void CloseStore(X509Store store)
        {
            if (store == null)
            {
                return;
            }
            try
            {
                store.Close();
            }
            catch
            {
            }
        }

        private static X509Certificate2 FindInStore(X509Store store)
        {
            if (store == null)
            {
                return null;
            }
            X509Certificate2Collection list =
                store.Certificates.Find(X509FindType.FindByThumbprint, ExpectedThumbprint, false);
            if (list.Count == 0)
            {
                return null;
            }
            return list[0];
        }
    }
}
