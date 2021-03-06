﻿using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace IrdLibraryClient.POCOs
{
    public sealed class SearchResult
    {
        public List<SearchResultItem>? Data;
        public int Draw;

        [JsonPropertyName("recordsFiltered")]
        public int RecordsFiltered;

        [JsonPropertyName("recordsTotal")]
        public int RecordsTotal;
    }

    public sealed class SearchResultItem
    {
        public string? Id; // product code
        public string? AppVersion;
        public string? GameVersion;
        public string? UpdateVersion;
        public string? Date;
        public string? Title; // <span class="text-success glyphicon glyphicon-ok-sign"></span> MLB® 15 The Show™
        public string? Filename; // <a class="btn btn-primary btn-xs" href="ird/BCUS00236-7ECC6C2A9C12DABB875342DFF80E9A97.ird">Download</a>\r\n                <a class="btn btn-info btn-xs" href="info.php?file=ird/BCUS00236-7ECC6C2A9C12DABB875342DFF80E9A97.ird"><span class="glyphicon glyphicon-info-sign" ></span></a>
        public string? State; //always 1?
    }
}
