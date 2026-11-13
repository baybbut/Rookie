using System.IO.Compression;
using System.Security;
using System.Text;
namespace Rookie.Data;
// Ghi file .xlsx tối giản (không cần thư viện ngoài): mỗi sheet gồm dòng tiêu đề (in đậm) và các dòng dữ liệu; số được ghi dạng số.
public record XSheet(string Name, string[] Header, List<object?[]> Rows);
public static class Xlsx {
 public static byte[] Build(params XSheet[] sheets) {
  using var ms = new MemoryStream();
  using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, true)) {
   Add(zip, "[Content_Types].xml", "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Default Extension=\"xml\" ContentType=\"application/xml\"/><Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/><Override PartName=\"/xl/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml\"/>" + string.Concat(sheets.Select((_, i) => $"<Override PartName=\"/xl/worksheets/sheet{i + 1}.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>")) + "</Types>");
   Add(zip, "_rels/.rels", "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/></Relationships>");
   Add(zip, "xl/workbook.xml", "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><sheets>" + string.Concat(sheets.Select((s, i) => $"<sheet name=\"{SecurityElement.Escape(SafeName(s.Name))}\" sheetId=\"{i + 1}\" r:id=\"rId{i + 1}\"/>")) + "</sheets></workbook>");
   Add(zip, "xl/_rels/workbook.xml.rels", "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" + string.Concat(sheets.Select((_, i) => $"<Relationship Id=\"rId{i + 1}\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet{i + 1}.xml\"/>")) + $"<Relationship Id=\"rId{sheets.Length + 1}\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/></Relationships>");
   Add(zip, "xl/styles.xml", "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><styleSheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><fonts count=\"2\"><font><sz val=\"11\"/><name val=\"Calibri\"/></font><font><b/><sz val=\"11\"/><name val=\"Calibri\"/></font></fonts><fills count=\"2\"><fill><patternFill patternType=\"none\"/></fill><fill><patternFill patternType=\"gray125\"/></fill></fills><borders count=\"1\"><border><left/><right/><top/><bottom/><diagonal/></border></borders><cellStyleXfs count=\"1\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\"/></cellStyleXfs><cellXfs count=\"3\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\"/><xf numFmtId=\"0\" fontId=\"1\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyFont=\"1\"/><xf numFmtId=\"3\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyNumberFormat=\"1\"/></cellXfs></styleSheet>");
   for (var i = 0; i < sheets.Length; i++) Add(zip, $"xl/worksheets/sheet{i + 1}.xml", Sheet(sheets[i]));
  }
  return ms.ToArray();
 }

 static string SafeName(string n) { var s = new string(n.Where(c => !"[]:*?/\\".Contains(c)).ToArray()); return s.Length > 31 ? s[..31] : s; }
 static void Add(ZipArchive zip, string path, string xml) { using var w = new StreamWriter(zip.CreateEntry(path).Open(), new UTF8Encoding(false)); w.Write(xml); }
 static string Col(int i) => ((char)('A' + i)).ToString();

 static string Sheet(XSheet s) {
  var all = new List<(object?[] Row, bool Head)> { (s.Header, true) }; all.AddRange(s.Rows.Select(r => (r, false)));
  var width = Enumerable.Range(0, s.Header.Length).Select(c => Math.Min(60, Math.Max(10, all.Max(x => c < x.Row.Length ? Convert.ToString(x.Row[c], System.Globalization.CultureInfo.InvariantCulture)?.Length ?? 0 : 0) + 3))).ToArray();
  var sb = new StringBuilder("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?><worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><cols>");
  for (var c = 0; c < width.Length; c++) sb.Append($"<col min=\"{c + 1}\" max=\"{c + 1}\" width=\"{width[c]}\" customWidth=\"1\"/>");
  sb.Append("</cols><sheetData>");
  for (var r = 0; r < all.Count; r++) {
   sb.Append($"<row r=\"{r + 1}\">");
   for (var c = 0; c < all[r].Row.Length; c++) {
    var v = all[r].Row[c]; var cell = $"{Col(c)}{r + 1}";
    if (v is null) continue;
    if (!all[r].Head && v is int or long or decimal or double) sb.Append($"<c r=\"{cell}\" s=\"2\"><v>{Convert.ToString(v, System.Globalization.CultureInfo.InvariantCulture)}</v></c>");
    else sb.Append($"<c r=\"{cell}\" t=\"inlineStr\"{(all[r].Head ? " s=\"1\"" : "")}><is><t xml:space=\"preserve\">{SecurityElement.Escape(Convert.ToString(v, System.Globalization.CultureInfo.InvariantCulture))}</t></is></c>");
   }
   sb.Append("</row>");
  }
  return sb.Append("</sheetData></worksheet>").ToString();
 }
}
