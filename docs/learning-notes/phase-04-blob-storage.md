# Phase 4 — Blob Storage (product images)

## AZ-204 objectives covered
- Develop solutions that use Blob Storage
- Manage container access levels and anonymous access

## 1. Business scenario

Products had no visual identifier — just code, name, price, reorder level.
This phase adds an optional product image, uploaded through the existing
Create/Edit forms and stored in Azure Blob Storage, with the blob's URL
saved on the `Product` record and displayed as a thumbnail on the Products
list.

### Access model: public container, on purpose, for now
Two secure alternatives were considered and deliberately deferred:
- **SAS tokens** (short-lived signed URLs generated per request) — a real,
  valid middle ground, but would add a second access-security lesson before
  the first one (Phase 3's auth) had even settled.
- **Managed Identity** — already scheduled as its own phase (Phase 9),
  where it will secure *both* Azure SQL and Blob Storage together in one
  clean move. Pulling it in now, just for images, would repeat exactly the
  "why'd you jump ahead" confusion from Phase 5.

So this phase intentionally keeps the simplest option: the container is
public, `Blob anonymous access` is enabled at the storage-account level,
and the container's own access level is `Blob` (anonymous read on blobs,
not full account listing). Good enough for non-sensitive product images;
explicitly **not** the pattern for anything sensitive — that's what Phase 9
exists to fix, deliberately, later.

## 2. Azure resource created

**Storage Account** `niroinventorystorage`, Standard performance, **LRS**
replication (cheapest tier — this project is running on a 1-month/$200
Azure free trial, so every resource choice defaults to the cheapest option
that still does the job). Primary service: **Azure Blob Storage** (not Data
Lake Storage Gen2 — hierarchical namespace left disabled; Data Lake Gen2 is
built for big-data/analytics workloads, real overkill for a handful of
product images). One container, `product-images`, with **Anonymous access
level: Blob**.

## 3. Code

- **Added**: `Azure.Storage.Blobs` NuGet package.
- **`Product.cs`**: one new property, `string? ImageUrl`.
- **Live schema change**: `ALTER TABLE dbo.Products ADD ImageUrl NVARCHAR(500) NULL`
  run directly against the live Azure SQL database (safe — purely additive,
  nullable, doesn't touch existing rows), and mirrored in `sql/schema.sql`
  for fresh setups.
- **`ProductData.cs`**: `ImageUrl` added to the `SELECT`/`INSERT`/`UPDATE`
  statements and the `Map`/`AddParameters` helpers, same pattern as every
  other column — `reader.IsDBNull(5) ? null : reader.GetString(5)` on read,
  `(object?)product.ImageUrl ?? DBNull.Value` on write, since the column is
  nullable.
- **Added `Data/ProductImageStorage.cs`** — a small class wrapping
  `BlobContainerClient`. `UploadAsync(IFormFile file)` generates a random
  blob name (`Guid.NewGuid()` + the original file extension, so uploads
  never collide and the original filename is never trusted/exposed), uploads
  the file's stream with its real `ContentType` preserved
  (`BlobUploadOptions { HttpHeaders = new BlobHttpHeaders { ContentType = file.ContentType } }`
  — without this, blobs default to `application/octet-stream`, which still
  often *displays* fine in an `<img>` tag via browser sniffing, but is the
  wrong metadata to leave on the blob), and returns the blob's public URL.
- **Registered as a `Singleton`** in `Program.cs` — a deliberate difference
  from `ProductData`/`WarehouseData`/`InventoryData`, which are `Scoped`.
  `BlobContainerClient` is documented by Microsoft as safe and intended to
  be reused across requests (it manages its own connection pooling
  internally), unlike a `SqlConnection`, which is opened and disposed once
  per call. Using the wrong lifetime isn't a bug either way at this scale,
  but matching each SDK's actual documented usage pattern is worth doing
  deliberately, not by habit.
- **`ProductsController`**: `Create`/`Edit` POST actions gained an optional
  `IFormFile? imageFile` parameter. If provided, `ProductImageStorage.UploadAsync`
  runs and its returned URL overwrites `product.ImageUrl` before saving;
  if omitted, whichever value was already on the model is kept as-is (see
  the hidden field note below).
- **Views**: `Create.cshtml`/`Edit.cshtml` forms gained
  `enctype="multipart/form-data"` (required for any file upload) and a
  `<input type="file" name="imageFile">`. `Edit.cshtml` also needed a
  `<input type="hidden" asp-for="ImageUrl" />` — without it, submitting the
  edit form with no new file would post a *blank* `ImageUrl` and silently
  wipe the existing image, since nothing on the form would carry the old
  value forward. `Index.cshtml` shows a 48px thumbnail per row, or "No
  image" text when `ImageUrl` is null.

## 4. Azure Portal configuration

1. **Create the Storage Account**: same resource group/region as the
   existing App Service. Standard performance, LRS replication, primary
   service **Azure Blob Storage**.
2. **Enable public blob access at the account level**: during creation (or
   afterward via **Configuration → Allow Blob anonymous access → Enabled**).
   This is an account-wide gate — even a container set to "Public: Blob"
   won't actually serve anonymous requests if this account-level switch is
   off.
3. **Create the container**: **Data storage → Containers → + Container**,
   name `product-images`, **Anonymous access level: Blob**.
4. **Get the connection string**: **Security + networking → Access keys →
   Show → copy Connection string**.
5. Store it as `Storage:ConnectionString` in **User Secrets** locally
   (`dotnet user-secrets set "Storage:ConnectionString" "..."`), and as an
   App Service **Application setting** (`Storage__ConnectionString`) for the
   live deployment — same pattern as every other secret in this project.
   `Storage:ContainerName` (`product-images`) isn't sensitive, so it lives
   directly in `appsettings.json`, committed.

## 5. Testing

**Verified live**, not mocked — logged in, uploaded a real product through
the Create form with an image attached, confirmed:
- The blob actually landed in the `product-images` container with a random
  GUID-based name.
- `Product.ImageUrl` was saved correctly in Azure SQL.
- The Products list page rendered the thumbnail via a direct `<img src>` to
  the blob URL.
- The blob URL is genuinely publicly reachable with **no authentication at
  all** — fetched it from a fresh, cookie-less HTTP client and got `200 OK`.
- After fixing an initial oversight (blobs uploaded with the default
  `application/octet-stream` content type instead of the real one), re-verified
  the corrected upload path returns the proper `image/png` content type.

**Automated:** existing `dotnet test` suite (11 tests) still passes
unchanged — none of it touches product images, so no new test coverage was
added for this phase specifically; verification was manual/live only, same
as the initial JWT and Functions phases before their automated tests
existed.

**Cleanup note:** the three test products created during manual
verification (`TEST-IMG-001/002/003`) were deleted from the live database
afterward; two small test-image blobs were left behind in the container as
harmless orphans (a few bytes each, no longer referenced by anything) rather
than spending time on ad-hoc blob-deletion tooling for something this low-stakes.

---
**Previous phase:** [Phase 3 — Authentication](phase-03-authentication.md)
**Next phase:** [Phase 5 — Azure Functions](phase-05-azure-functions.md) *(already completed, out of order)*
