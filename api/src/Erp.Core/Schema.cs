namespace Erp.Core;

/// <summary>The ERP exports the cockpit understands.</summary>
public enum Dataset { Articles, Suppliers, StockMovements, SalesOrders, PurchaseOrders }

public enum FieldType { Text, Whole, Number, Date, Choice }

/// <summary>
/// A target field of a dataset. <see cref="Synonyms"/> are column names seen in real exports
/// (German ERP systems, Excel lists, English templates); they drive the automatic column mapping.
/// </summary>
public sealed record Field(
    string Key,
    string Label,
    FieldType Type,
    bool Required,
    string[] Synonyms,
    string[]? Choices = null,
    bool AllowNegative = false);

public sealed record DatasetSchema(Dataset Dataset, string Label, string Description, Field[] Fields)
{
    public Field this[string key] => Fields.First(f => f.Key == key);
}

public static class Schemas
{
    public static readonly string[] MovementTypes = ["Zugang", "Abgang", "Korrektur"];

    public static readonly DatasetSchema Articles = new(Dataset.Articles, "Artikelstamm",
        "Artikel mit Warengruppe, Einheit und Einstandspreis. Bestehende Artikel werden aktualisiert.",
    [
        new("number", "Artikelnummer", FieldType.Text, true, ["artikelnr", "artikelnummer", "art nr", "artnr", "materialnummer", "material", "sachnummer", "item", "item no", "sku", "part number"]),
        new("name", "Bezeichnung", FieldType.Text, true, ["bezeichnung", "artikelbezeichnung", "beschreibung", "kurztext", "name", "description"]),
        new("group", "Warengruppe", FieldType.Text, true, ["warengruppe", "artikelgruppe", "gruppe", "kategorie", "produktgruppe", "category", "group"]),
        new("unit", "Einheit", FieldType.Text, true, ["einheit", "me", "bme", "mengeneinheit", "basismengeneinheit", "basiseinheit", "unit", "uom"]),
        new("unitCost", "Einstandspreis (€)", FieldType.Number, true, ["einstandspreis", "ek", "ek preis", "einkaufspreis", "preis", "standardpreis", "kosten", "unit cost", "cost"]),
        new("supplierNumber", "Hauptlieferant", FieldType.Text, false, ["lieferant", "lieferantennr", "hauptlieferant", "kreditor", "kreditorennr", "supplier"]),
    ]);

    public static readonly DatasetSchema Suppliers = new(Dataset.Suppliers, "Lieferanten",
        "Lieferantenstamm. Bestehende Lieferanten werden aktualisiert.",
    [
        new("number", "Lieferantennummer", FieldType.Text, true, ["lieferantennr", "lieferantennummer", "kreditor", "kreditorennr", "kreditorennummer", "lieferant nr", "supplier no", "vendor"]),
        new("name", "Name", FieldType.Text, true, ["name", "firma", "lieferant", "firmenname", "bezeichnung", "company"]),
        new("city", "Ort", FieldType.Text, false, ["ort", "stadt", "city", "standort"]),
        new("country", "Land", FieldType.Text, false, ["land", "country", "laenderkennzeichen", "lkz"]),
    ]);

    public static readonly DatasetSchema StockMovements = new(Dataset.StockMovements, "Lagerbewegungen",
        "Zu- und Abgänge je Artikel. Abgänge mit negativer Menge oder Bewegungsart „Abgang“.",
    [
        new("articleNumber", "Artikelnummer", FieldType.Text, true, ["artikelnr", "artikelnummer", "artnr", "material", "materialnummer", "sachnummer", "item", "sku"]),
        new("date", "Buchungsdatum", FieldType.Date, true, ["buchungsdatum", "datum", "belegdatum", "bewegungsdatum", "date", "posting date"]),
        new("quantity", "Menge", FieldType.Number, true, ["menge", "anzahl", "stueck", "qty", "quantity", "bewegungsmenge"], AllowNegative: true),
        new("type", "Bewegungsart", FieldType.Choice, true, ["bewegungsart", "buchungsart", "art", "vorgang", "type"], MovementTypes),
        new("reference", "Beleg", FieldType.Text, false, ["beleg", "belegnr", "belegnummer", "referenz", "auftrag", "reference"]),
    ]);

    public static readonly DatasetSchema SalesOrders = new(Dataset.SalesOrders, "Kundenaufträge",
        "Auftragspositionen. Ohne Lieferdatum gelten sie als offen (Auftragsrückstand).",
    [
        new("orderNumber", "Auftragsnummer", FieldType.Text, true, ["auftragsnr", "auftragsnummer", "auftrag", "belegnr", "order", "order no", "sales order"]),
        new("lineNo", "Position", FieldType.Whole, true, ["position", "pos", "posnr", "zeile", "line"]),
        new("customer", "Kunde", FieldType.Text, true, ["kunde", "kundenname", "debitor", "auftraggeber", "customer"]),
        new("articleNumber", "Artikelnummer", FieldType.Text, true, ["artikelnr", "artikelnummer", "artnr", "material", "item", "sku"]),
        new("quantity", "Menge", FieldType.Number, true, ["menge", "anzahl", "auftragsmenge", "qty", "quantity"]),
        new("unitPrice", "Verkaufspreis (€)", FieldType.Number, true, ["preis", "vk", "vk preis", "verkaufspreis", "einzelpreis", "price", "unit price"]),
        new("orderDate", "Auftragsdatum", FieldType.Date, true, ["auftragsdatum", "belegdatum", "datum", "order date"]),
        new("dueDate", "Liefertermin", FieldType.Date, true, ["liefertermin", "wunschtermin", "termin", "faellig", "faelligkeit", "due date", "delivery date"]),
        new("shippedDate", "Lieferdatum", FieldType.Date, false, ["lieferdatum", "geliefert am", "versanddatum", "ausgeliefert", "shipped", "shipped date"]),
    ]);

    public static readonly DatasetSchema PurchaseOrders = new(Dataset.PurchaseOrders, "Bestellungen",
        "Bestellpositionen bei Lieferanten. Grundlage für die Liefertreue.",
    [
        new("orderNumber", "Bestellnummer", FieldType.Text, true, ["bestellnr", "bestellnummer", "bestellung", "belegnr", "po", "po number", "purchase order"]),
        new("lineNo", "Position", FieldType.Whole, true, ["position", "pos", "posnr", "zeile", "line"]),
        new("supplierNumber", "Lieferantennummer", FieldType.Text, true, ["lieferant", "lieferantennr", "kreditor", "kreditorennr", "supplier", "vendor"]),
        new("articleNumber", "Artikelnummer", FieldType.Text, true, ["artikelnr", "artikelnummer", "artnr", "material", "item", "sku"]),
        new("quantity", "Menge", FieldType.Number, true, ["menge", "bestellmenge", "anzahl", "qty", "quantity"]),
        new("orderDate", "Bestelldatum", FieldType.Date, true, ["bestelldatum", "belegdatum", "datum", "order date"]),
        new("promisedDate", "Bestätigter Termin", FieldType.Date, true, ["bestaetigter termin", "liefertermin", "ab termin", "termin", "zugesagt", "promised date", "confirmed date"]),
        new("receivedDate", "Wareneingang", FieldType.Date, false, ["wareneingang", "we datum", "eingangsdatum", "geliefert am", "received", "received date"]),
    ]);

    public static readonly IReadOnlyList<DatasetSchema> All = [Articles, Suppliers, StockMovements, SalesOrders, PurchaseOrders];

    public static DatasetSchema For(Dataset d) => All.First(s => s.Dataset == d);
}
