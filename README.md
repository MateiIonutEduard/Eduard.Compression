# Eduard.Compression
This is a library that compress files and build an archives of these files.<br>
My format is <b>FDZ</b> (<b>F</b>ast <b>D</b>evil <b>Z</b>ip), doesn't use file encryption and not provides <br>
support for error detection like other archive formats.

# Installation Steps
Open Visual Studio or MonoDevelop go to <b>Build</b> menu:<br>
 1. Go to <b>Properties</b> > <b>Build</b> menu.<br>
 2. Change Project Configuration from <b>Debug</b> to <b>Release</b> if not already set.<br>
 3. Verify that the documentation is generated, otherwise set it.<br>
 4. Select <b>Build Solution</b>

# Creates archives and read data from it:
```csharp
static void Main(string[] args) {
  if(args.Length != 4)
    Environment.Exit(-1);
  
  if(args[1] == "-c") {
     Queue<string> queue = new Queue<string>();
     string parent = Path.GetDirectoryName(args[2]);
     queue.Enqueue(args[2]);
     
     string root = Path.GetFileName(args[2]);
     DevilArchive arc = new DevilArchive(Path.Combine(parent, "//", args[3]), FileAccess.Write);
     
     while(queue.Count > 0) {
       string file = queue.Dequeue();
       string path = file.Replace(parent, root + "//");
       
       string ext = Path.GetExtension(file);
       
       if(string.IsNullOrEmpty(ext)) {
          DevilFolder folder = new DevilFolder(path);
          arc.Entries.Add(folder);
       } else {
          FileStream fs = new FileStream(file, FileMode.Open);
          MemoryStream ms = new MemoryStream();
          
          DevilStream ds = new DevilStream(ms, DevilAccess.Compress);
          byte[] buffer = new byte[8192];
          int len = 0;
          
          while ((len = fs.Read(buffer, 0, buffer.Length)) != 0)
            ds.Write(buffer, 0, len);

          ds.Close();
          
          DevilFile file = new DevilFile(path);
          file.Length = (uint)fs.Length;
          
          file.stream = new MemoryStream(ms.ToArray());
          arc.Entries.Add(file);
          fs.Close();
       }
       // so it is director, we will go through it.
       if(string.IsNullOrEmpty(ext)) {
           foreach (string str in Directory.GetFiles(file))
             queue.Enqueue(str);
             
           foreach (string str in Directory.GetDirectories(file))
             queue.Enqueue(str);
       }
     }
     // close the archive now.
     arc.Close();
  } else {
     string parent = Path.GetDirectoryName(args[2]);
     DevilArchive arc = new DevilArchive(args[2], FileAccess.Read);
     
     foreach (DevilEntry entry in arc.Entries) {
        if(entry is DevilFolder) {
          DevilFolder folder = entry as DevilFolder;
          folder.Combine(parent);
          // just create directory on disk.
          folder.CreateEntry();
        } else {
          DevilFile file = entry as DevilFile;
          file.Combine(parent);
          
          FileStream fs = new FileStream(file.GetPath(), FileMode.Create);
          DevilStream ds = new DevilStream(file.stream, DevilAccess.Extract);
          
          int len = 0;
          byte[] buffer = new byte[8192];
          uint size = file.Length;
          uint total = 0;
          
          while((len = ds.Read(buffer, 0, buffer.Length)) != 0)
            fs.Write(buffer, 0, len);
          
          // close the streams.
          ds.Close();
          fs.Close();
        }
     }
     
     // close archive stream.
     arc.Close();
  }
}
```
