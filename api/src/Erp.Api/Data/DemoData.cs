namespace Erp.Api.Data;

/// <summary>
/// Two years of a fictional machine builder's spare-parts business, simulated week by week:
/// demand per article, reorders when stock runs low, suppliers that are more or less punctual,
/// sales orders behind most issues and an open order backlog. Deterministic (fixed seed) and
/// relative to <c>today</c>, so the dashboards always show "the last 12 months".
/// All companies and people are fictional.
/// </summary>
public static class DemoData
{
    public sealed record DemoSet(
        List<Article> Articles, List<Supplier> Suppliers, List<StockMovement> Movements,
        List<SalesOrderLine> SalesOrders, List<PurchaseOrderLine> PurchaseOrders);

    private sealed record SupplierProfile(string Number, string Name, string City, double OnTime, int LeadDays);

    private static readonly SupplierProfile[] SupplierProfiles =
    [
        new("L-100", "Hydrotec Nordrhein GmbH", "Duisburg", 0.95, 12),
        new("L-110", "Pneumatik Brandt KG", "Münster", 0.92, 8),
        new("L-120", "Elektro Stahlberg GmbH", "Dortmund", 0.97, 6),
        new("L-130", "Antriebe Westfalia AG", "Bielefeld", 0.86, 18),
        new("L-140", "Lagertechnik Vossen GmbH", "Krefeld", 0.94, 10),
        new("L-150", "Normteile Kessler GmbH", "Wuppertal", 0.99, 4),
        new("L-160", "Schmierstoffe Hansen GmbH", "Hamburg", 0.96, 7),
        new("L-170", "Sensorik Arndt GmbH", "Essen", 0.71, 14),
        new("L-180", "Dichtungstechnik Lambers GmbH", "Bocholt", 0.93, 9),
        new("L-190", "Kunststoffe Terhorst GmbH", "Rhede", 0.90, 11),
        new("L-200", "Werkzeugbau Ostendorf GmbH", "Borken", 0.84, 16),
        new("L-210", "Industriebedarf Brinkmann GmbH", "Coesfeld", 0.98, 5),
    ];

    private sealed record GroupProfile(string Name, string Supplier, string Unit, decimal MinCost, decimal MaxCost, string[] Items);

    private static readonly GroupProfile[] Groups =
    [
        new("Hydraulik", "L-100", "Stk", 18, 640, ["Hydraulikzylinder Ø40", "Hydraulikzylinder Ø63", "Hydraulikschlauch DN10", "Hydraulikschlauch DN12", "Wegeventil 4/3", "Druckbegrenzungsventil", "Rückschlagventil", "Filterelement 10 µm", "Schnellkupplung", "Manometer 0–250 bar", "Drosselventil", "Hydraulikpumpe 8 cm³", "Druckspeicher 1 l", "Dichtungssatz Ø50", "Verschraubung 12L"]),
        new("Pneumatik", "L-110", "Stk", 6, 320, ["Pneumatikzylinder Ø32", "Pneumatikzylinder Ø50", "Magnetventil 5/2", "Wartungseinheit G1/2", "Druckluftschlauch 8 mm", "Steckverschraubung 8 mm", "Schalldämpfer G1/4", "Druckregler", "Drosselrückschlagventil", "Vakuumsauger Ø40", "Ventilinsel 4-fach", "Kupplungsdose", "Druckschalter", "Schlauchschelle", "Zylinderschalter"]),
        new("Elektrik", "L-120", "Stk", 1.2m, 260, ["Schütz 24 V DC", "Motorschutzschalter 4–6 A", "Feinsicherung 10 A", "Relais 24 V", "Reihenklemme 4 mm²", "Not-Aus-Taster", "Netzteil 24 V 5 A", "Leitungsschutzschalter B16", "Frequenzumrichter 1,5 kW", "Steuerleitung 5×1,5 (m)", "Leuchtmelder grün", "Kabelverschraubung M20", "Hauptschalter 25 A", "Klemmkasten", "Signalsäule"]),
        new("Antriebstechnik", "L-130", "Stk", 9, 980, ["Getriebemotor 0,55 kW", "Zahnriemen HTD 8M-30", "Keilriemen SPZ 1250", "Klauenkupplung", "Kettenrad 1/2\"", "Rollenkette 1/2\" (m)", "Spannrolle", "Flanschlager", "Welle Ø25", "Passfeder 8×7", "Zahnriemenscheibe", "Elastomerkranz", "Getriebeöl (l)", "Bremsmotor 0,75 kW", "Gelenkwelle"]),
        new("Lager & Dichtungen", "L-140", "Stk", 0.8m, 190, ["Kugellager 6205-2RS", "Kugellager 6306-2RS", "Pendelrollenlager 22210", "Wellendichtring 40×62×7", "O-Ring-Sortiment", "Stehlager UCP205", "Nadellager", "Gleitlager Ø30", "Axiallager 51106", "Radialwellendichtring 30×47", "Flachdichtung DN50", "Dichtschnur (m)", "Lagerfett 400 g", "Spannhülse H210", "Abstreifer Ø40"]),
        new("Normteile", "L-150", "Stk", 0.05m, 4.5m, ["Schraube M8×30 8.8", "Schraube M10×40 8.8", "Mutter M8", "Mutter M10", "Unterlegscheibe 8,4", "Sicherungsring 25", "Zylinderstift 6×20", "Gewindestange M12 (m)", "Federring 10", "Senkschraube M6×16", "Splint 4×40", "Hutmutter M8", "Spannstift 5×30", "Ringschraube M12", "Kabelbinder 200 mm"]),
        new("Betriebsstoffe", "L-160", "l", 2.4m, 58, ["Hydrauliköl HLP 46", "Kühlschmierstoff-Konzentrat", "Schmierfett Lithium", "Kriechöl", "Reinigungstücher (Pkg)", "Bremsenreiniger", "Getriebeöl CLP 220", "Korrosionsschutz", "Handwaschpaste", "Industriereiniger", "Kettenspray", "Silikonspray", "Ölbindemittel (kg)", "Kontaktspray", "Druckluftöl"]),
        new("Sensorik", "L-170", "Stk", 14, 420, ["Näherungsschalter M12 PNP", "Näherungsschalter M18", "Lichtschranke", "Drucksensor 0–10 bar", "Temperaturfühler PT100", "Drehgeber 1024 Imp.", "Füllstandssensor", "Zylinderschalter Reed", "Lichtgitter", "Ultraschallsensor", "Durchflusssensor", "Sicherheitsschalter", "Endschalter", "Kapazitiver Sensor", "Encoder-Kabel 5 m"]),
    ];

    private static readonly string[] Customers =
    [
        "Aawerk Kunststofftechnik GmbH", "Hohenhorst Landmaschinen KG", "Issel Präzisionsteile GmbH", "Westmünsterland Druckguss AG",
        "Berkelwerk Verpackung GmbH", "Dinkel Maschinenbau GmbH", "Stadtlohner Möbelwerke KG", "Vredener Textil AG",
    ];

    public static DemoSet Generate(DateOnly today) => Generate(today, Workspace.Template);

    public static DemoSet Generate(DateOnly today, Guid workspace)
    {
        var rnd = new Random(2026);
        var start = today.AddDays(-730);
        var articles = new List<Article>();
        var movements = new List<StockMovement>();
        var sales = new List<SalesOrderLine>();
        var purchases = new List<PurchaseOrderLine>();
        var suppliers = SupplierProfiles.Select(s => new Supplier { WorkspaceId = workspace, Number = s.Number, Name = s.Name, City = s.City, Country = "DE" }).ToList();
        int soSeq = 0, poSeq = 0;

        for (var g = 0; g < Groups.Length; g++)
        {
            var group = Groups[g];
            for (var i = 0; i < group.Items.Length; i++)
            {
                var number = $"A-{11 + g}{i + 1:000}";
                // Two of the 15 articles per group are supplied by a second, regional supplier.
                var supplierNo = i % 7 == 6 ? SupplierProfiles[8 + (g % 4)].Number : group.Supplier;
                var supplier = SupplierProfiles.First(s => s.Number == supplierNo);
                var cost = Math.Round(group.MinCost + (decimal)Math.Pow(rnd.NextDouble(), 2) * (group.MaxCost - group.MinCost), 2);
                var unit = group.Unit == "l" && group.Items[i].Contains("(Pkg)", StringComparison.Ordinal) ? "Pkg" : group.Unit;
                articles.Add(new Article { WorkspaceId = workspace, Number = number, Name = group.Items[i], Group = group.Name, Unit = unit, UnitCost = cost, SupplierNumber = supplierNo });

                // Cheap parts move in large quantities, expensive ones rarely.
                var weekly = Math.Max(0.15, 40.0 / Math.Sqrt((double)cost + 1) * (0.4 + rnd.NextDouble()));
                // Some articles stopped selling 7–14 months ago but still have stock: the slow movers.
                var stopsAt = rnd.NextDouble() < 0.14 ? today.AddDays(-rnd.Next(210, 420)) : DateOnly.MaxValue;
                var lot = Math.Max(1, (int)Math.Ceiling(weekly * rnd.Next(4, 9)));
                var reorderPoint = Math.Max(1, (int)Math.Ceiling(weekly * (supplier.LeadDays / 7.0 + 1.5)));
                decimal stock = lot + reorderPoint;
                movements.Add(Move(workspace, number, start, stock, "Korrektur", "Eröffnungsbestand"));
                DateOnly? openPoReceipt = null;
                PurchaseOrderLine? openPo = null;

                for (var week = start; week <= today; week = week.AddDays(7))
                {
                    // Stock that arrives this week can only be issued from its arrival day on.
                    var earliestIssue = week;
                    // Receipt of an outstanding order.
                    if (openPo is not null && openPoReceipt <= week.AddDays(6) && openPoReceipt <= today)
                    {
                        if (openPoReceipt > earliestIssue) earliestIssue = openPoReceipt.Value;
                        movements.Add(Move(workspace, number, openPoReceipt!.Value, openPo.Quantity, "Zugang", openPo.OrderNumber));
                        stock += openPo.Quantity;
                        openPo.ReceivedDate = openPoReceipt;
                        openPo = null;
                    }

                    if (week < stopsAt)
                    {
                        var qty = Poisson(rnd, weekly);
                        if (qty > 0)
                        {
                            qty = (int)Math.Min(qty, stock);
                            var day = week.AddDays(rnd.Next(0, 5));
                            if (day < earliestIssue) day = earliestIssue;
                            if (qty > 0 && day <= today)
                            {
                                // Most issues ship a customer order, the rest is internal consumption.
                                string reference;
                                if (rnd.NextDouble() < 0.35)
                                {
                                    var order = $"AU-{day:yy}{++soSeq:0000}";
                                    var ordered = day.AddDays(-rnd.Next(3, 25));
                                    var due = ordered.AddDays(rnd.Next(7, 21));
                                    sales.Add(new SalesOrderLine
                                    {
                                        WorkspaceId = workspace, OrderNumber = order, LineNo = 1, Customer = Customers[rnd.Next(Customers.Length)], ArticleNumber = number,
                                        Quantity = qty, UnitPrice = Math.Round(cost * (decimal)(1.35 + rnd.NextDouble() * 0.45), 2),
                                        OrderDate = ordered, DueDate = due, ShippedDate = day,
                                    });
                                    reference = order;
                                }
                                else reference = $"FA-{day:yy}{rnd.Next(1000, 9999)}";
                                movements.Add(Move(workspace, number, day, -qty, "Abgang", reference));
                                stock -= qty;
                            }
                        }
                    }

                    // Reorder when stock falls to the reorder point (only while the article still sells).
                    if (openPo is null && stock <= reorderPoint && week < stopsAt && week < today.AddDays(-3))
                    {
                        var ordered = week.AddDays(rnd.Next(0, 4));
                        var promised = ordered.AddDays(supplier.LeadDays + rnd.Next(-2, 3));
                        var late = rnd.NextDouble() > supplier.OnTime;
                        var receipt = late ? promised.AddDays(rnd.Next(1, supplier.OnTime < 0.8 ? 21 : 10)) : promised.AddDays(-rnd.Next(0, 3));
                        openPo = new PurchaseOrderLine
                        {
                            WorkspaceId = workspace, OrderNumber = $"BE-{ordered:yy}{++poSeq:0000}", LineNo = 1, SupplierNumber = supplierNo, ArticleNumber = number,
                            Quantity = lot, OrderDate = ordered, PromisedDate = promised,
                        };
                        openPoReceipt = receipt < ordered ? ordered.AddDays(1) : receipt;
                        purchases.Add(openPo);
                    }

                    // Annual stocktaking: small corrections.
                    if (week.Month == 12 && week.Day <= 7 && rnd.NextDouble() < 0.3 && stock > 2)
                    {
                        var diff = rnd.Next(-2, 2);
                        if (diff != 0) { movements.Add(Move(workspace, number, week.AddDays(2), diff, "Korrektur", $"INV-{week:yyyy}")); stock += diff; }
                    }
                }
            }
        }

        // Open backlog: recent orders, some already overdue.
        var active = articles.Where((_, i) => i % 3 != 2).ToList();
        for (var n = 0; n < 34; n++)
        {
            var ordered = today.AddDays(-rnd.Next(2, 40));
            var order = $"AU-{ordered:yy}{++soSeq:0000}";
            var lines = rnd.Next(1, 4);
            var customer = Customers[rnd.Next(Customers.Length)];
            var due = ordered.AddDays(rnd.Next(12, 80));
            for (var l = 1; l <= lines; l++)
            {
                var a = active[rnd.Next(active.Count)];
                sales.Add(new SalesOrderLine
                {
                    WorkspaceId = workspace, OrderNumber = order, LineNo = l, Customer = customer, ArticleNumber = a.Number,
                    Quantity = rnd.Next(1, a.UnitCost < 5 ? 200 : 12),
                    UnitPrice = Math.Round(a.UnitCost * (decimal)(1.35 + rnd.NextDouble() * 0.45), 2),
                    OrderDate = ordered, DueDate = due,
                });
            }
        }

        return new DemoSet(articles, suppliers, movements, sales, purchases);
    }

    private static StockMovement Move(Guid workspace, string article, DateOnly date, decimal qty, string type, string reference) =>
        new() { WorkspaceId = workspace, ArticleNumber = article, Date = date, Quantity = qty, Type = type, Reference = reference };

    private static int Poisson(Random rnd, double mean)
    {
        // Knuth for small means, normal approximation for large ones.
        if (mean > 30) return Math.Max(0, (int)Math.Round(mean + Math.Sqrt(mean) * Gaussian(rnd)));
        var l = Math.Exp(-mean);
        var k = 0;
        var p = 1.0;
        do { k++; p *= rnd.NextDouble(); } while (p > l);
        return k - 1;
    }

    private static double Gaussian(Random rnd) =>
        Math.Sqrt(-2 * Math.Log(1 - rnd.NextDouble())) * Math.Cos(2 * Math.PI * rnd.NextDouble());
}
