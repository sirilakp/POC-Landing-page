# Product Requirements Document (PRD)
## Roosterplanner — Dutch Educational Scheduling & Timetabling Platform
**Version:** 1.0  
**Date:** 2025  
**Status:** Draft  
**Owner:** Product Team  
**Classification:** Internal — Confidential

---

## Table of Contents

1. [Executive Summary](#1-executive-summary)
2. [Problem Statement](#2-problem-statement)
3. [Target Users & Personas](#3-target-users--personas)
4. [User Stories & Acceptance Criteria](#4-user-stories--acceptance-criteria)
5. [Functional Requirements](#5-functional-requirements)
6. [Non-Functional Requirements](#6-non-functional-requirements)
7. [Feature Prioritization (MoSCoW)](#7-feature-prioritization-moscow)
8. [Success Metrics & KPIs](#8-success-metrics--kpis)

---

## 1. Executive Summary

**Roosterplanner** is a cloud-native, enterprise-grade educational scheduling and timetabling platform purpose-built for Dutch educational institutions operating under MBO (Middelbaar Beroepsonderwijs), HBO (Hoger Beroepsonderwijs), and WO (Wetenschappelijk Onderwijs) regulations. The platform consolidates roster management, room/resource planning, curriculum coordination, and student information system (SIS) integration into a single, coherent application hosted on Microsoft Azure.

Dutch educational institutions face unique scheduling complexity driven by:
- Multi-cohort, multi-track curriculum structures (opleidingen, trajecten, keuzedelen)
- Regulatory reporting obligations (DUO, BPV registrations, urennormering)
- High room utilisation demands with heterogeneous resource constraints
- Diverse stakeholder views (student, teacher/docent, planner/roostermaker, management)
- Mandatory integration with multiple SIS platforms (Osiris, Eduarte, AFAS HR, BRON)

Roosterplanner delivers a scalable, multi-tenant SaaS platform that replaces manual Excel-based scheduling and fragmented legacy tools with an intelligent, conflict-aware scheduling engine, real-time notifications, and rich analytics — enabling institutions to maximise resource efficiency, improve student experience, and maintain regulatory compliance.

### Key Value Propositions

| Stakeholder | Value |
|---|---|
| Roostermakers / Planners | 80% reduction in manual conflict resolution time; drag-and-drop scheduling with automated constraint checking |
| Students | Single personalised timetable view across all opleidingen and keuzedelen; push/email change alerts |
| Docenten / Teachers | Real-time schedule visibility; availability self-management; workload balancing reports |
| Management / CvB | Utilisation dashboards; FTE analysis; regulatory compliance reporting |
| IT / Beheerders | Azure-hosted, zero-infrastructure-management; BYOD-friendly; Entra ID SSO |

---

## 2. Problem Statement

### 2.1 Current Landscape

Dutch educational institutions — particularly MBO and HBO — rely on a fragmented mix of:

- **Legacy desktop scheduling tools** (aSc Timetables, Untis) that lack cloud collaboration
- **Manual Excel rosters** shared via email or SharePoint, with no conflict detection
- **Point-in-time SIS exports** that go stale within hours
- **Separate room booking systems** (EMS, Outlook resource calendars) disconnected from the academic schedule
- **No unified student-facing view** — students must cross-reference multiple sources

### 2.2 Core Pain Points

**For Roostermakers (Planners):**
- Identifying all hard and soft conflicts when placing a lesactiviteit (lesson event) requires manually checking room availability, teacher availability, student group (klas/cohort) availability, and equipment requirements simultaneously
- Changes cascade unpredictably — moving one lesactiviteit can invalidate dozens of others
- Last-minute changes (docent ziek, ruimte defect) have no automated notification chain
- No audit trail for who changed what and why

**For Docenten:**
- No self-service availability declaration (beschikbaarheid opgeven) — everything goes through the roostermaker via email
- Workload (urennorm) visibility is limited; overscheduling is only detected manually
- No mobile-optimised view of their schedule

**For Students:**
- Students in deeltijd (part-time) or duaal (dual) trajecten have highly individualised schedules not easily derivable from a klas-level roster
- No personalised view that merges onderwijsgroep, keuzedeel, and BPV (Beroepspraktijkvorming) blocks
- No real-time change notifications

**For Management:**
- Room utilisation data is not available in real time; post-hoc analysis requires manual Excel aggregation
- FTE/SWV (Salarisweegvorm / Normjaartaak) compliance reporting is error-prone
- No cross-institutional (multi-locatie) scheduling view

### 2.3 Opportunity

A purpose-built, cloud-native platform that understands Dutch educational domain concepts natively — schooljaar, periode, lesweek, onderwijsgroep, opleiding, keuzedeel, BPV-blok, SWV-norm — and integrates directly with the SIS systems already in place, can eliminate the pain points above while providing a foundation for AI-assisted scheduling optimisation.

---

## 3. Target Users & Personas

### 3.1 Persona 1 — De Roostermaker (Scheduler/Planner)

| Attribute | Detail |
|---|---|
| **Role** | Centrally plans all lesson events for one or more opleidingen or teams |
| **Institution type** | MBO (large ROC) or HBO (hogeschool) |
| **Technical proficiency** | Medium — comfortable with scheduling tools, not a developer |
| **Daily tools** | Excel, Outlook, legacy scheduling software, SIS portal |
| **Primary goal** | Create a conflict-free rooster for the upcoming periode within deadline |
| **Pain points** | Manual conflict checking; late availability info from docenten; last-minute room changes |
| **Key needs** | Drag-and-drop scheduler; real-time conflict detection; bulk import from SIS; change audit log |

**Scenario:** Linda is a roostermaker at a large ROC with 8,000 students across 40 opleidingen. She must publish roosters for periode 3 (starting week 5) by the end of week 3. She needs to assign 1,200 lesactiviteiten to rooms and time slots, respecting teacher availability declarations, room capacities, equipment requirements, and klas-level non-overlaps. She currently spends 3 full days on conflict resolution alone.

### 3.2 Persona 2 — De Docent (Teacher/Lecturer)

| Attribute | Detail |
|---|---|
| **Role** | Delivers lessons; may also coordinate a module or opleiding |
| **Institution type** | MBO, HBO, or WO |
| **Technical proficiency** | Low to medium |
| **Daily tools** | Outlook, SIS portal, Teams |
| **Primary goal** | Know where to be and when; manage personal availability |
| **Pain points** | Outdated paper/PDF roosters; no push notifications for changes; no visibility into own workload hours |
| **Key needs** | Personal schedule view; availability self-declaration; iCal subscription; mobile access |

### 3.3 Persona 3 — De Student

| Attribute | Detail |
|---|---|
| **Role** | Enrolled in one or more opleidingen, may have individuele leerwegen or keuzedelen |
| **Institution type** | MBO (voltijd, deeltijd, duaal), HBO |
| **Technical proficiency** | High (digital native) |
| **Daily tools** | Mobile phone, Teams, SIS student portal |
| **Primary goal** | Know my personal schedule for the week; receive changes immediately |
| **Pain points** | Klas-level rooster doesn't reflect individual keuzedelen or BPV-blokken; no push notifications |
| **Key needs** | Personalised schedule; push/email alerts; iCal sync with phone calendar; BPV-blok visibility |

### 3.4 Persona 4 — De Teamleider / Opleidingscoördinator (Programme Coordinator)

| Attribute | Detail |
|---|---|
| **Role** | Manages curriculum delivery for one or more opleidingen; accountable for contact hours |
| **Institution type** | MBO, HBO |
| **Technical proficiency** | Medium |
| **Daily tools** | SIS, Excel, management reports |
| **Primary goal** | Ensure sufficient contacturen are scheduled per periode; manage team workload |
| **Key needs** | Contacturen dashboard; SWV/FTE utilisation report; curriculum coverage view |

### 3.5 Persona 5 — De Systeembeheerder (IT Administrator)

| Attribute | Detail |
|---|---|
| **Role** | Manages the platform configuration, user provisioning, SIS integrations |
| **Institution type** | Central IT department |
| **Technical proficiency** | High |
| **Daily tools** | Azure Portal, Entra ID, API management tools |
| **Primary goal** | Keep the platform running, maintain data integrations, manage tenants |
| **Key needs** | Multi-tenant admin portal; SIS integration configuration; audit logs; health monitoring |

### 3.6 Persona 6 — De Beheerder Roosters / Roostercoördinator (Scheduling Coordinator)

| Attribute | Detail |
|---|---|
| **Role** | Senior planner who owns scheduling policy, approves published roosters, manages constraints |
| **Institution type** | MBO / HBO central planning office |
| **Technical proficiency** | Medium–High |
| **Primary goal** | Define institution-wide constraints; approve and publish period roosters; manage exceptions |
| **Key needs** | Constraint management UI; approval workflow; bulk publish; change impact analysis |

---

## 4. User Stories & Acceptance Criteria

### 4.1 Roostermaker Stories

---

**US-RM-001: Drag-and-drop lesson placement**

> *As a roostermaker, I want to drag a lesactiviteit from an unscheduled pool onto a time grid, so that I can place it quickly and see conflicts immediately.*

**Acceptance Criteria:**
- AC1: The scheduler shows a week-grid (maandag–vrijdag, configurable start/end times per institution)
- AC2: Unscheduled lesactiviteiten are listed in a side panel, filterable by opleiding, team, docent, klas
- AC3: Dragging a lesactiviteit over a time slot shows a colour-coded preview (green = no conflict, amber = soft conflict, red = hard conflict)
- AC4: Dropping onto a red slot is blocked with a modal explaining the conflict reason(s)
- AC5: Dropping onto an amber slot prompts the roostermaker to confirm, listing the soft conflict details
- AC6: The placement is saved within 2 seconds and the grid refreshes
- AC7: An audit log entry is created with user, timestamp, lesactiviteit ID, old slot, new slot

---

**US-RM-002: Conflict detection and resolution**

> *As a roostermaker, I want the system to automatically detect hard and soft conflicts when I schedule a lesactiviteit, so that I don't have to manually cross-check availability.*

**Acceptance Criteria:**
- AC1: Hard conflicts detected: room double-booking, docent double-booking, klas/onderwijsgroep double-booking, room capacity exceeded, missing required equipment
- AC2: Soft conflicts detected: docent scheduled outside declared availability window, room on different locatie than preferred, back-to-back lessons exceeding max consecutive hours, lessons scheduled in a pauze-blok
- AC3: Conflict reasons are shown in human-readable Dutch ("Docent Jan de Vries is al ingeroosterd in zaal B-204 op dit tijdstip")
- AC4: Batch conflict scan can be triggered on the full rooster; results exported to Excel
- AC5: Conflicts are re-evaluated in real time as the rooster changes

---

**US-RM-003: Bulk scheduling via import**

> *As a roostermaker, I want to import a prepared Excel template of lesactiviteiten with proposed time slots, so that I can schedule a full periode in bulk rather than one-by-one.*

**Acceptance Criteria:**
- AC1: A downloadable Excel template is available with required columns (lesactiviteit ID, datum, start, eind, zaal, docent)
- AC2: On upload, the system validates all rows and reports errors per row before committing
- AC3: Rows with hard conflicts are rejected; rows with only soft conflicts are imported with a warning flag
- AC4: A preview screen shows the full import result before final commit
- AC5: Import is transactional — partial failure does not partially commit
- AC6: Import of 2,000 rows completes within 30 seconds

---

**US-RM-004: Room finder / suggestion**

> *As a roostermaker, I want the system to suggest available rooms that meet the requirements of a lesactiviteit, so that I don't have to manually search room calendars.*

**Acceptance Criteria:**
- AC1: The room finder shows rooms filtered by: capacity ≥ group size, required equipment present, locatie preference, building preference
- AC2: Available slots for each room are shown visually for the selected week
- AC3: Rooms are ranked by fit score (capacity match, equipment match, proximity to other lessons of the same klas)
- AC4: Selecting a suggestion pre-fills the slot in the grid for confirmation
- AC5: Room finder results update within 1 second of changing filter criteria

---

**US-RM-005: Publishing a periode-rooster**

> *As a roostermaker, I want to publish a completed rooster for a periode, so that students and docenten receive notifications and can view their schedules.*

**Acceptance Criteria:**
- AC1: A rooster can have status: Concept, Ter Review, Gepubliceerd, Gearchiveerd
- AC2: Publishing triggers notifications to all affected students and docenten (configurable: email, push, Teams)
- AC3: Only users with the Roostercoördinator role can publish (roostermakers can submit for review)
- AC4: Once published, changes trigger a "Roosterwijziging" notification to affected parties
- AC5: The published rooster is visible in the public-facing student and docent views
- AC6: A publication log records who published, when, and how many lesactiviteiten were included

---

### 4.2 Docent Stories

---

**US-DOC-001: View personal schedule**

> *As a docent, I want to view my personal rooster for any given week or periode, so that I always know where I need to be.*

**Acceptance Criteria:**
- AC1: Default view is the current week; navigation to past and future weeks is available
- AC2: Each lesactiviteit shows: time, zaal (with locatie and floor), onderwijsgroep/klas, vak/module naam, type (hoorcollege, werkcollege, toets, etc.)
- AC3: Cancelled or changed lesactiviteiten are visually distinguished (strikethrough + amber/red highlight)
- AC4: View is accessible on mobile (responsive, Bootstrap 5)
- AC5: Schedule loads within 1 second for a 4-week window

---

**US-DOC-002: Declare availability (beschikbaarheid opgeven)**

> *As a docent, I want to declare my availability and unavailability for upcoming perioden, so that the roostermaker can plan around my constraints.*

**Acceptance Criteria:**
- AC1: Docent can mark time blocks as: Beschikbaar, Niet beschikbaar, Voorkeur (preferred), Te vermijden (avoid if possible)
- AC2: Recurring patterns can be set (e.g., every Tuesday afternoon unavailable)
- AC3: A reason field is available (optional, visible to roostermaker only)
- AC4: Declarations are submitted within a configurable deadline window per periode
- AC5: After the deadline, changes require a reason and trigger a notification to the roostermaker
- AC6: The roostermaker sees all declarations in a consolidated docent-availability matrix view

---

**US-DOC-003: iCal subscription**

> *As a docent, I want to subscribe to my rooster via iCal, so that my schedule appears in Outlook/Google Calendar automatically.*

**Acceptance Criteria:**
- AC1: A unique, private iCal URL is generated per docent
- AC2: The feed includes all scheduled lesactiviteiten with location, description, and attendees
- AC3: Cancelled events appear as cancelled (STATUS:CANCELLED) in the feed
- AC4: The URL can be regenerated (invalidating the old one) if compromised
- AC5: Feed response time < 500ms for a full schooljaar

---

### 4.3 Student Stories

---

**US-STU-001: View personalised schedule**

> *As a student, I want to view my personalised rooster that combines my onderwijsgroep lessons, keuzedelen, and BPV-blokken, so that I have one complete picture of my obligations.*

**Acceptance Criteria:**
- AC1: Schedule is derived from the student's SIS enrollment (onderwijsgroep, keuzedelen, individuele leerwegen)
- AC2: BPV-blokken are shown as full-day or multi-day blocks with bedrijfsnaam (if populated)
- AC3: View toggles: Day, Week, Month, List (agenda)
- AC4: Free/busy periods (roostervrij, vakantie, studiedagen) are visually blocked
- AC5: Student can filter by vak/module
- AC6: No SIS data is directly exposed — only scheduling-relevant fields

---

**US-STU-002: Receive change notifications**

> *As a student, I want to receive a notification immediately when my rooster changes, so that I am never surprised by a cancelled or moved lesson.*

**Acceptance Criteria:**
- AC1: Notifications sent via: in-app, email, optionally push (PWA) within 5 minutes of a published change
- AC2: Notification content: what changed (lesactiviteit name, old time/room → new time/room or "VERVALT"), reason (if provided by roostermaker)
- AC3: Students can configure notification preferences (which channels, minimum lead time)
- AC4: A notification history is accessible in-app for 90 days
- AC5: Notifications are batched if multiple changes happen within 10 minutes (configurable)

---

**US-STU-003: iCal sync**

> *As a student, I want to subscribe to my personal rooster via iCal, so that my phone/tablet calendar stays in sync.*

**Acceptance Criteria:**  
- Same as US-DOC-003 but scoped to student enrollment
- AC1: iCal URL is unique per student and respects privacy (no other students' data)

---

### 4.4 Systeembeheerder Stories

---

**US-ADM-001: Multi-tenant institution onboarding**

> *As a systeembeheerder, I want to onboard a new institution (instelling) to the platform, so that their users can start using Roosterplanner with their own data isolated from other instellingen.*

**Acceptance Criteria:**
- AC1: A new instelling is created with: naam, BRIN-nummer, instellingtype (MBO/HBO/WO), logo, primary colour, locaties
- AC2: Data isolation is enforced at the database level (tenant ID on all tables, row-level security)
- AC3: The instelling's Azure Entra ID tenant is linked for SSO
- AC4: A default set of roles (roostermaker, docent, student, beheerder) is provisioned
- AC5: The onboarding wizard completes in < 15 minutes
- AC6: A test user can log in via SSO within 30 minutes of onboarding completion

---

**US-ADM-002: SIS integration configuration**

> *As a systeembeheerder, I want to configure the SIS integration for my instelling, so that student, docent, opleiding, and onderwijsgroep data is automatically synchronised.*

**Acceptance Criteria:**
- AC1: Supported SIS systems: Osiris, Eduarte, AFAS HR, BRON (read-only), Magister (MBO), Somtoday
- AC2: Integration config includes: endpoint URL, authentication credentials (OAuth2/API key, stored in Azure Key Vault), sync schedule (cron), field mapping
- AC3: A test connection button verifies credentials before saving
- AC4: Sync results (records imported, updated, errors) are logged and viewable
- AC5: Credential rotation does not interrupt sync — the new credential is validated before the old one is deactivated

---

## 5. Functional Requirements

### 5.1 Module: User Management & Authentication

| ID | Requirement | Priority |
|---|---|---|
| FR-UM-001 | Support Azure Entra ID (AAD) as the primary identity provider via OpenID Connect | Must Have |
| FR-UM-002 | Support local fallback authentication for institutions without Entra ID | Should Have |
| FR-UM-003 | Role-Based Access Control (RBAC) with roles: SuperAdmin, InstellingBeheerder, Roostercoördinator, Roostermaker, Docent, Student, Gast | Must Have |
| FR-UM-004 | Support custom role definitions per instelling | Could Have |
| FR-UM-005 | User profile management: naam, email, foto, functie, team, locatie-voorkeur | Must Have |
| FR-UM-006 | Group membership managed via Entra ID groups or manual assignment | Must Have |
| FR-UM-007 | Impersonation capability for beheerders (with full audit log) | Should Have |
| FR-UM-008 | MFA enforcement configurable per instelling | Must Have |
| FR-UM-009 | SCIM 2.0 provisioning endpoint for automated user lifecycle management | Should Have |
| FR-UM-010 | Session timeout configurable per instelling (default: 8 hours for docenten, 4 hours for students) | Must Have |

### 5.2 Module: Scheduling Engine

| ID | Requirement | Priority |
|---|---|---|
| FR-SE-001 | Lesactiviteiten (lesson events) have: vak, module, type, duur (in minuten), required klas/onderwijsgroep, required docent(en), required zaal-type, required equipment, preferred locatie | Must Have |
| FR-SE-002 | Hard constraint engine: double-booking (docent, klas, zaal), capacity exceeded, equipment unavailable | Must Have |
| FR-SE-003 | Soft constraint engine: docent availability preference, room locatie preference, consecutive lesson limits, pauze buffer | Must Have |
| FR-SE-004 | Constraint weighting — soft constraints have configurable penalty scores | Should Have |
| FR-SE-005 | Semi-automated scheduling: suggest top-3 time/room combinations for an unscheduled lesactiviteit | Should Have |
| FR-SE-006 | Fully automated scheduling: batch-schedule a full periode using a constraint-satisfaction solver | Could Have |
| FR-SE-007 | Conflict report: list all conflicts in the current rooster with severity and resolution suggestions | Must Have |
| FR-SE-008 | Swap functionality: swap two lesactiviteiten's time slots with conflict re-evaluation | Must Have |
| FR-SE-009 | Copy rooster from previous periode/schooljaar with date-offset | Must Have |
| FR-SE-010 | Rooster templates (vaste roosters) that repeat on a weekly pattern | Should Have |
| FR-SE-011 | Exception scheduling: mark specific lesweek instances as afwijkend (different room, cancelled, extra lesson) | Must Have |
| FR-SE-012 | Lesroostervrije perioden (study weeks, exam weeks, vacation blocks) defined at instelling level | Must Have |
| FR-SE-013 | Multi-split groups: a lesactiviteit can be split across multiple parallel sub-groups in different rooms | Should Have |
| FR-SE-014 | Linked activities: a hoorcollege linked to mandatory werkcolleges (student must attend both) | Could Have |
| FR-SE-015 | Scheduling horizon: support planning up to 2 schooljaren ahead | Must Have |

### 5.3 Module: Room & Resource Management

| ID | Requirement | Priority |
|---|---|---|
| FR-RR-001 | Room (zaal) attributes: naam, code, locatie, gebouw, verdieping, capaciteit (standaard/examen), type (lokaal, collegezaal, lab, sportzaal, aula), equipment list | Must Have |
| FR-RR-002 | Equipment/resource types: beamer, whiteboard, smartboard, computer (per seat count), toegankelijkheid (wheelchair), stilteruimte, etc. | Must Have |
| FR-RR-003 | Room availability calendar: permanent unavailability (onderhoud, verbouwing) and ad-hoc blocks | Must Have |
| FR-RR-004 | Room booking for non-lesson purposes (vergadering, evenement) by authorised users | Should Have |
| FR-RR-005 | Room utilisation report: percentage bezetting per zaal, per week, per periode | Must Have |
| FR-RR-006 | Floor plan upload (image/SVG) for locatie overview with room clickability | Could Have |
| FR-RR-007 | Room swap suggestion when a room becomes unavailable (finds best alternative) | Should Have |
| FR-RR-008 | Multi-locatie support: same instelling has multiple campussen (Amsterdam, Utrecht, etc.) | Must Have |
| FR-RR-009 | Virtual/online rooms for hybrid and remote lessons (with Teams/Zoom link field) | Must Have |
| FR-RR-010 | Resource (equipment) booking separate from room (e.g., mobile laptop cart, recording equipment) | Could Have |

### 5.4 Module: Timetable Views

| ID | Requirement | Priority |
|---|---|---|
| FR-TV-001 | Student view: personalised week/day/month/list view derived from onderwijsgroep + keuzedelen enrollment | Must Have |
| FR-TV-002 | Docent view: personal schedule view with own-lesson highlighting | Must Have |
| FR-TV-003 | Klas/Onderwijsgroep view: all lessons for a specific klas or onderwijsgroep | Must Have |
| FR-TV-004 | Zaal view: all lessons scheduled in a specific room for a period | Must Have |
| FR-TV-005 | Opleiding view: all lessons for an entire opleiding across all klassen | Must Have |
| FR-TV-006 | Locatie/Building overview: room-by-room grid for a building showing occupancy | Should Have |
| FR-TV-007 | Team/vakgroep view: all lessons taught by a team of docenten | Should Have |
| FR-TV-008 | Print-ready PDF export of any view | Must Have |
| FR-TV-009 | Public (unauthenticated) view for selected opleidingen (configurable per instelling) | Could Have |
| FR-TV-010 | Comparison view: show old vs. new rooster side-by-side after a change | Should Have |
| FR-TV-011 | Colour coding configurable: by vak, by type, by docent, by klas | Should Have |
| FR-TV-012 | Werkweek / Roosterweek numbering display (ISO week numbers and custom instelling week numbers) | Must Have |

### 5.5 Module: Curriculum Planning

| ID | Requirement | Priority |
|---|---|---|
| FR-CP-001 | Opleiding (programme) management: naam, CROHO-code, niveau (MBO 2/3/4, HBO-B, HBO-M, WO-B, WO-M), duur (in jaren/semesters) | Must Have |
| FR-CP-002 | Schooljaar and periode definition: start/end dates, aantal weken, vakantieperioden, toetsperioden | Must Have |
| FR-CP-003 | Module/vak catalog: naam, studiepunten/EC (ECTS), contacturen per week, jaar/semester, SBU (Studie Belasting Uren) | Must Have |
| FR-CP-004 | Onderwijsgroep (klas) management: naam, opleiding, cohort (instroom jaar), track (voltijd/deeltijd/duaal), max groepsgrootte, docent-coach | Must Have |
| FR-CP-005 | Keuzedeel management (MBO-specific): keuzedeel code (CK code), naam, sector, koppelingen aan kwalificatiePOCs | Must Have |
| FR-CP-006 | BPV-blok planning: student-level BPV periods with bedrijf, begindatum, einddatum, BPV-begeleider | Must Have |
| FR-CP-007 | Contacturen tracking: scheduled vs. required contacturen per vak per periode, with compliance indicator | Must Have |
| FR-CP-008 | SWV (taakbelasting) tracking for docenten: ingeroosterde uren vs. norm per periode | Should Have |
| FR-CP-009 | Curriculum template: clone an opleiding's curriculum to a new cohort with adjustments | Must Have |
| FR-CP-010 | Toetsrooster (exam schedule): separate scheduling of toetsen with stricter conflict rules | Must Have |
| FR-CP-011 | Studiedagen and PLG (Professionele LeergGemeenschap) day scheduling | Should Have |
| FR-CP-012 | Deeltijd-specifieke roosters: evening/weekend scheduling support | Must Have |

### 5.6 Module: Notifications & Alerts

| ID | Requirement | Priority |
|---|---|---|
| FR-NA-001 | Notification channels: in-app, email (Azure Communication Services), push (PWA/Web Push) | Must Have |
| FR-NA-002 | Notification types: roosterwijziging (change), lesuitval (cancellation), nieuwe lesactiviteit, ruimtewijziging, toetswijziging | Must Have |
| FR-NA-003 | Notification targeting: by student, by docent, by klas, by opleiding, by locatie, broadcast | Must Have |
| FR-NA-004 | Notification preferences: user-configurable per type and channel | Must Have |
| FR-NA-005 | Change lead-time alerting: alert roostermakers when a change is published within N hours of the lesson | Must Have |
| FR-NA-006 | Daily digest option: bundle notifications into a daily/weekly digest email | Should Have |
| FR-NA-007 | Microsoft Teams integration: post changes to a Teams channel for a klas or team | Should Have |
| FR-NA-008 | Notification log: all sent notifications with delivery status (delivered, bounced, read) | Must Have |
| FR-NA-009 | Emergency broadcast: roostercoördinator can send urgent message to all users of a locatie | Should Have |
| FR-NA-010 | Quiet hours: notifications suppressed between configurable hours (default 22:00–07:00) | Should Have |

### 5.7 Module: Import/Export & SIS Integration

| ID | Requirement | Priority |
|---|---|---|
| FR-IE-001 | iCal export (RFC 5545): personal rooster for student/docent; room calendar; opleiding calendar | Must Have |
| FR-IE-002 | iCal subscription URL (live feed, refreshes on access) | Must Have |
| FR-IE-003 | Excel export: rooster in tabular form, configurable columns, filtered by any dimension | Must Have |
| FR-IE-004 | Excel import: bulk lesactiviteit scheduling from template | Must Have |
| FR-IE-005 | PDF export: print-ready roosters for posting (A4, A3 landscape) | Must Have |
| FR-IE-006 | CSV import/export for all master data (docenten, ruimten, vakken, klassen) | Must Have |
| FR-IE-007 | Osiris integration: import studenten, inschrijvingen, vakken, groepen via Osiris REST API | Must Have |
| FR-IE-008 | Eduarte integration: import curricula, klassen, docenten, BPV-data via Eduarte API or SOAP | Must Have |
| FR-IE-009 | AFAS HR integration: import docenten, contracturen, contracttypes via AFAS REST API (GetConnector) | Should Have |
| FR-IE-010 | BRON integration (read-only): validate student enrollments against BRON deelnemers register | Should Have |
| FR-IE-011 | Somtoday integration: import lesgroepen, leerlingen, docenten via Somtoday REST API | Could Have |
| FR-IE-012 | Magister integration: bidirectional sync of roosters with Magister | Could Have |
| FR-IE-013 | Webhook outbound: publish schedule changes to external systems via configurable webhooks | Should Have |
| FR-IE-014 | OData API endpoint: expose schedule data for Power BI and other BI tools | Should Have |
| FR-IE-015 | IMS Global OneRoster 1.2 API: industry-standard roster exchange | Could Have |

### 5.8 Module: Admin Portal

| ID | Requirement | Priority |
|---|---|---|
| FR-AP-001 | Super-admin portal for managing all instellingen (tenant management) | Must Have |
| FR-AP-002 | Instelling-level admin: manage users, roles, locaties, SIS integrations, notification settings | Must Have |
| FR-AP-003 | Audit log viewer: searchable log of all data changes, user actions, system events | Must Have |
| FR-AP-004 | Data retention policy management: configure how long historical roosters are retained | Should Have |
| FR-AP-005 | Feature flags: enable/disable specific features per instelling | Must Have |
| FR-AP-006 | Theming: instelling logo, primary colour, custom domain (CNAME) | Should Have |
| FR-AP-007 | Maintenance mode: take instelling offline for maintenance with custom message | Must Have |
| FR-AP-008 | Usage dashboard: active users, API call volume, storage consumption per instelling | Must Have |
| FR-AP-009 | SIS sync status dashboard: last sync time, record counts, error summary per integration | Must Have |
| FR-AP-010 | Backup and restore: trigger manual backup; restore data to a previous point-in-time | Should Have |

### 5.9 Module: Reporting & Analytics

| ID | Requirement | Priority |
|---|---|---|
| FR-RA-001 | Room utilisation report: % bezetting per zaal, per gebouw, per locatie, per periode | Must Have |
| FR-RA-002 | Docent workload report: geplande uren vs. norm (SWV/FTE) per docent, per periode | Must Have |
| FR-RA-003 | Contacturen report: geplande contacturen vs. vereiste contacturen per vak/module | Must Have |
| FR-RA-004 | Roosterwijzigingen report: number and type of changes per periode, trend over time | Should Have |
| FR-RA-005 | Lesuitval report: cancelled lessons per opleiding, per docent, per vak | Must Have |
| FR-RA-006 | BPV-coverage report: student BPV periods, coverage by begeleider | Should Have |
| FR-RA-007 | Peak load analysis: busiest hours/days per locatie per periode | Could Have |
| FR-RA-008 | Power BI integration via OData endpoint | Should Have |
| FR-RA-009 | Scheduled reports: email delivery of reports on a cron schedule | Should Have |
| FR-RA-010 | Custom report builder: ad-hoc query builder for advanced users | Could Have |

---

## 6. Non-Functional Requirements

### 6.1 Performance

| ID | Requirement | Target |
|---|---|---|
| NFR-P-001 | Page load time (timetable view) | < 1.5 seconds (P95) |
| NFR-P-002 | Conflict detection response time (single lesactiviteit) | < 500ms (P99) |
| NFR-P-003 | Batch conflict scan (full periode, 2,000 lesactiviteiten) | < 30 seconds |
| NFR-P-004 | iCal feed response | < 500ms (P95) |
| NFR-P-005 | Search (room/docent/klas lookup) | < 300ms (P99) |
| NFR-P-006 | API response time (read endpoints) | < 200ms (P95) |
| NFR-P-007 | API response time (write endpoints) | < 500ms (P95) |
| NFR-P-008 | SIS sync throughput | ≥ 10,000 records/minute |
| NFR-P-009 | Concurrent users per instelling (large ROC: 8,000 students) | 2,000 concurrent |
| NFR-P-010 | Total platform concurrent users | 50,000 concurrent |

### 6.2 Availability & Reliability

| ID | Requirement | Target |
|---|---|---|
| NFR-A-001 | Platform availability (monthly) | 99.9% (< 44 min/month downtime) |
| NFR-A-002 | Planned maintenance window | Sundays 02:00–04:00 CET |
| NFR-A-003 | Recovery Time Objective (RTO) | < 1 hour |
| NFR-A-004 | Recovery Point Objective (RPO) | < 15 minutes |
| NFR-A-005 | No single point of failure in production architecture | Required |
| NFR-A-006 | Graceful degradation: read-only mode when write services are unavailable | Required |

### 6.3 Security

| ID | Requirement |
|---|---|
| NFR-S-001 | All data in transit encrypted with TLS 1.2+ |
| NFR-S-002 | All data at rest encrypted with AES-256 (Azure Storage Service Encryption, Transparent Data Encryption) |
| NFR-S-003 | Tenant data isolation: strict row-level security in Azure SQL; no cross-tenant data leakage |
| NFR-S-004 | OWASP Top 10 compliance — penetration test required before production launch |
| NFR-S-005 | All secrets stored in Azure Key Vault; no secrets in code or config files |
| NFR-S-006 | Audit log for all data mutations, immutable, retained for 7 years |
| NFR-S-007 | GDPR compliance: data subject access request (DSAR) support, right to erasure (pseudonymisation) |
| NFR-S-008 | AVG (Dutch GDPR implementation) compliance: verwerkingsregister maintained |
| NFR-S-009 | BIO (Baseline Informatiebeveiliging Overheid) alignment |
| NFR-S-010 | Vulnerability scanning on container images and dependencies (automated in CI/CD) |
| NFR-S-011 | Azure DDoS Protection Standard enabled |
| NFR-S-012 | Content Security Policy (CSP) headers enforced |

### 6.4 Scalability

| ID | Requirement |
|---|---|
| NFR-SC-001 | Horizontal scaling of application tier via Azure App Service auto-scale |
| NFR-SC-002 | Database tier: Azure SQL Hyperscale or Elastic Pool with read replicas |
| NFR-SC-003 | Stateless application design (session state in Azure Redis Cache) |
| NFR-SC-004 | Multi-region deployment ready (primary: West Europe; secondary: North Europe) |
| NFR-SC-005 | Asynchronous processing for all long-running operations (SIS sync, batch scheduling, PDF generation) via Azure Service Bus |

### 6.5 Usability & Accessibility

| ID | Requirement |
|---|---|
| NFR-U-001 | WCAG 2.1 Level AA compliance |
| NFR-U-002 | Responsive design: desktop (1920px+), tablet (768px), mobile (375px+) |
| NFR-U-003 | Support for Chrome (latest), Firefox (latest), Edge (latest), Safari 15+ |
| NFR-U-004 | Interface language: Dutch (primary), English (secondary), configurable per instelling |
| NFR-U-005 | Keyboard navigation support for all critical scheduling functions |

### 6.6 Maintainability

| ID | Requirement |
|---|---|
| NFR-M-001 | Full CI/CD pipeline with automated testing (unit, integration, E2E) |
| NFR-M-002 | Code coverage ≥ 80% for scheduling engine |
| NFR-M-003 | API versioning (URL versioning: /api/v1/, /api/v2/) with minimum 12-month deprecation window |
| NFR-M-004 | Infrastructure as Code (Bicep/Terraform) for all Azure resources |
| NFR-M-005 | Observability: structured logging (Application Insights), distributed tracing, custom metrics |

---

## 7. Feature Prioritization (MoSCoW)

### Must Have (MVP — Release 1.0)
- Entra ID SSO and RBAC
- Lesactiviteit management (CRUD)
- Hard conflict detection (room, docent, klas double-booking)
- Drag-and-drop scheduling grid
- Room management
- Student, docent, klas, and room views
- iCal export
- PDF/Excel export
- Publish workflow (concept → gepubliceerd)
- Email notifications for roosterwijzigingen
- Schooljaar/periode/vakantie configuration
- Onderwijsgroep/klas management
- Osiris integration (read)
- Excel bulk import
- Contacturen tracking
- Admin portal (user management, audit log)
- Multi-tenant support
- BPV-blok display

### Should Have (Release 1.1)
- Soft constraint engine with configurable weights
- Semi-automated room/slot suggestions
- Beschikbaarheid opgeven (docent availability)
- Microsoft Teams notifications
- Eduarte integration
- AFAS HR integration
- OData endpoint (Power BI)
- Workload (SWV) tracking dashboard
- Roosterwijzigingen report
- Custom theming per instelling
- SCIM provisioning
- Daily digest notifications

### Could Have (Release 1.2+)
- Fully automated scheduling solver (ILP/CP-SAT)
- Floor plan viewer
- Custom report builder
- Somtoday / Magister integration
- IMS Global OneRoster API
- Emergency broadcast system
- Resource (equipment) booking
- Linked activities (hoorcollege + werkcollege)
- Public unauthenticated view
- AI-assisted scheduling suggestions (Azure OpenAI)

### Won't Have (Out of Scope for v1.x)
- Native mobile apps (iOS/Android) — PWA sufficient
- Video conferencing integration (Teams/Zoom beyond link field)
- Financial/billing modules
- HR payroll integration beyond FTE reporting
- Student grade management (belongs in SIS)
- Full LMS functionality

---

## 8. Success Metrics & KPIs

### 8.1 Adoption Metrics

| KPI | Target (Year 1) | Measurement |
|---|---|---|
| Active instellingen on platform | ≥ 5 | Monthly active tenants |
| Daily Active Users (DAU) | ≥ 60% of enrolled users | Auth events per day |
| iCal subscription rate | ≥ 40% of docenten | iCal URL activations |
| Mobile access rate | ≥ 30% of student sessions | User-agent analytics |

### 8.2 Operational Efficiency Metrics

| KPI | Target | Measurement |
|---|---|---|
| Average time to publish a periode-rooster | Reduction of 50% vs. baseline | Time-to-publish log |
| Number of manual conflict resolutions per periode | Reduction of 80% | Conflict log |
| Roosterwijzigingen within 24h of lesson | < 5% of all changes | Change timestamp vs. lesson time |
| Room utilisation rate | ≥ 75% during lesperioden | Utilisation report |

### 8.3 Quality / Satisfaction Metrics

| KPI | Target | Measurement |
|---|---|---|
| Net Promoter Score (NPS) — roostermakers | ≥ 40 | Quarterly survey |
| NPS — students | ≥ 30 | Quarterly survey |
| Support ticket volume per instelling per month | < 20 | Helpdesk system |
| Platform uptime | ≥ 99.9% | Azure Monitor SLA dashboard |

### 8.4 Technical Metrics

| KPI | Target | Measurement |
|---|---|---|
| API P95 response time | < 200ms | Application Insights |
| Failed SIS sync rate | < 1% of sync jobs | Sync log |
| Security vulnerabilities (CVSS ≥ 7.0) unresolved > 7 days | 0 | Vulnerability scanner |
| Deployment frequency | ≥ 2/week | CI/CD pipeline |
| Mean Time to Recovery (MTTR) | < 30 minutes | Incident log |

---

*End of PRD — Roosterplanner v1.0*

*Next document: Technical Specification — see `TECHSPEC-Roosterplanner.md`*
