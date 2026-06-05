# Actie vereist — Graph-machtigingen toewijzen aan de Managed Identity van de App Service

**Project:** POC Landing Page  
**Aangevraagd door:** Sirilak Pompan  
**Datum:** 2026-05-30

---

## Achtergrond

Ik heb een nieuwe ASP.NET Core-webapplicatie geïmplementeerd in de testomgeving (`inhazuretest.onmicrosoft.com`). De app draait op Azure App Service met een **system-assigned Managed Identity**. De app gebruikt **Microsoft Graph** om externe gebruikers (B2B-gasten) uit te nodigen en hun applicatierolkoppelingen te beheren.

Hiervoor heeft de Managed Identity 4 **applicatiemachtigingen** (app-rollen) nodig op Microsoft Graph. Deze kunnen alleen worden toegewezen door iemand met de directoryfunctie **Toepassingsbeheerder**, **Beheerder voor bevoorrechte rollen** of **Globale beheerder** in `inhazuretest.onmicrosoft.com`.

Het implementatieaccount (`adm_sirilakp@inhazuretest.onmicrosoft.com`) heeft geen van deze functies en kan deze stap niet uitvoeren.

---

## Wat we u vragen te doen

Wijs de volgende 4 Microsoft Graph-machtigingen toe aan de hieronder genoemde Managed Identity.

### Managed Identity waaraan machtigingen moeten worden toegewezen

| Eigenschap | Waarde |
|---|---|
| Weergavenaam | `poclanding-app` (App Service) |
| Object-id | `0555224d-508c-43d9-a60a-06b265a0a85a` |
| Tenant | `inhazuretest.onmicrosoft.com` |

### Toe te wijzen machtigingen

| Naam van de machtiging | Type | App-rol-id |
|---|---|---|
| `User.Invite.All` | Applicatie | `09850681-111b-4a89-9bed-3f2cae46d706` |
| `User.Read.All` | Applicatie | `df021288-bdef-4463-88db-98f22de89214` |
| `AppRoleAssignment.ReadWrite.All` | Applicatie | `06b708a9-e830-4db3-a914-8e69da51d44f` |
| `Directory.Read.All` | Applicatie | `7ab1d382-f21e-4acd-a863-ba3e13f7da61` |

---

## Aanvullend verzoek — functie Toepassingsbeheerder voor ons account

Om in de toekomst te voorkomen dat we voor dit soort taken moeten escaleren, verzoeken we tevens om de directoryfunctie **Toepassingsbeheerder** toe te wijzen aan:

**`adm_sirilakp@inhazuretest.onmicrosoft.com`**

Met deze functie kunnen app-registraties en machtigingen voor service-principals worden beheerd zonder dat een Globale beheerder nodig is.

---

## Contact

Vragen? Neem contact op met Sirilak Pompan.

---

## Toelichting per machtiging — risico-uitleg voor de beheerder

Dit zijn **Microsoft Graph API-machtigingen**. Ze staan volledig los van Azure RBAC (de rollen Eigenaar/Inzender/Lezer op abonnementen en resourcegroepen). Het toewijzen van deze machtigingen geeft de app **geen** toegang tot Azure-infrastructuur — de app kan geen andere App Services, opslagaccounts, databases of andere resources in de Azure-portal aanraken. De Azure RBAC-rol van de Managed Identity is afzonderlijk tijdens de implementatie toegewezen.

### 1. `User.Invite.All` — Gemiddelde gevoeligheid, beperkt risico

**Wat het doet:** Hiermee kan de app B2B-gastuitnodigingen sturen naar externe e-mailadressen, waardoor een gastaccount in de tenant wordt aangemaakt.

**Waarom we het nodig hebben:** De kernfunctionaliteit van deze app is het uitnodigen van externe studenten of partners als gastgebruikers. Zonder deze machtiging kan er geen uitnodiging worden verstuurd.

**Risico:** De app kan gastaccounts aanmaken in de tenant. De app kan zichzelf niet uitnodigen in andere tenants, kan gasten niet verhogen naar beheerdersrollen en kan bestaande gebruikers niet wijzigen. Gastaccounts hebben standaard geen machtigingen totdat deze expliciet worden toegewezen.

---

### 2. `User.Read.All` — Lage gevoeligheid, alleen-lezen

**Wat het doet:** Hiermee kan de app het profiel van elke gebruiker (naam, e-mailadres, object-id) opzoeken in de directory.

**Waarom we het nodig hebben:** Na het uitnodigen van een gast moet de app de object-id van die gebruiker opzoeken om een app-rol toe te wijzen. De app controleert ook of een gast al bestaat voordat er opnieuw wordt uitgenodigd.

**Risico:** Alleen-lezen. De app kan gebruikersprofielen inzien, maar kan geen gebruiker wijzigen, verwijderen of zich als gebruiker voordoen. Dit is een van de veiligste Graph-machtigingen.

---

### 3. `AppRoleAssignment.ReadWrite.All` — Hoge gevoeligheid, kritisch te beoordelen

**Wat het doet:** Hiermee kan de app app-rolkoppelingen toewijzen aan of intrekken van service-principals in de tenant.

**Waarom we het nodig hebben:** Na het uitnodigen van een gast wijst de app een rol toe (bijvoorbeeld "Roosterplanner-gebruiker") aan de app-registratie. Zo bepaalt de app wat de gast binnen de applicatie mag doen.

**Risico:** Dit is de meest verstrekkende machtiging in deze lijst. In theorie zou de app rollen kunnen toewijzen aan *andere* apps in de tenant — niet alleen aan zichzelf. Het machtigingsmodel van Microsoft laat geen inperking toe tot één enkele app. Onze applicatiecode gebruikt deze machtiging uitsluitend voor de eigen app-registratie, maar de machtiging zelf geldt voor de hele tenant. Er wordt vertrouwen gesteld in onze applicatiecode om hier geen misbruik van te maken.

> Als de app ooit gecompromitteerd zou worden, zou deze machtiging een aanvaller in staat kunnen stellen gebruikers aan rollen in andere apps in de tenant te koppelen. Dit is de machtiging die kritisch beoordeeld dient te worden.

---

### 4. `Directory.Read.All` — Lage tot gemiddelde gevoeligheid, alleen-lezen

**Wat het doet:** Hiermee kan de app de volledige directory lezen — gebruikers, groepen, service-principals en app-registraties.

**Waarom we het nodig hebben:** Om tijdens het uitvoeren de service-principal-id van de eigen app-registratie op te zoeken (zodat de app weet aan welke resource rollen moeten worden gekoppeld) en om te verifiëren dat rolkoppelingen correct zijn toegepast.

**Risico:** Alleen-lezen. De app kan niets wijzigen. De app kan directoryobjecten opvragen, wat gevoelig is vanuit het oogpunt van gegevensblootstelling, maar er kan niets worden gewijzigd of verwijderd.

---

### Samenvatting

| Machtiging | Gevoeligheid | Kan gebruikers wijzigen? | Kan iets verwijderen? | Strikt noodzakelijk? |
|---|---|---|---|---|
| `User.Invite.All` | Gemiddeld | Alleen gasten aanmaken | Nee | Ja |
| `User.Read.All` | Laag | Nee | Nee | Ja |
| `AppRoleAssignment.ReadWrite.All` | **Hoog** | Rollen toewijzen | Nee | Ja |
| `Directory.Read.All` | Laag–gemiddeld | Nee | Nee | Ja |

### Deze machtigingen hebben geen invloed op Azure RBAC

| | Azure RBAC | Graph-applicatiemachtigingen |
|---|---|---|
| Beheert toegang tot | Azure-resources (VM's, App Service, Key Vault, enz.) | Microsoft Entra ID-gegevens (gebruikers, groepen, app-rollen) |
| Toegewezen op | Abonnementen / resourcegroepen / resources | Service-principals via Enterprise-toepassingen |
| Gebruikt door | Personen en Managed Identities die Azure-infrastructuur benaderen | Apps die de Microsoft Graph API aanroepen |
| Benodigde beheerdersrol | Eigenaar of Beheerder voor gebruikerstoegang | Toepassingsbeheerder of Globale beheerder |

Het toewijzen van deze 4 Graph-machtigingen heeft geen invloed op wat de app kan doen in de Azure-portal wat betreft resources. De twee systemen zijn volledig onafhankelijk van elkaar.
