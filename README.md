# GitLab Search — C# / .NET

بازنویسی ابزار [phillipj/gitlab-search](https://github.com/phillipj/gitlab-search) با C# و .NET 10، بدون وابستگی به Node.js یا بسته‌های NuGet در زمان اجرا.
همانند پروژه اصلی، این برنامه یک ابزار خط فرمان است، نه وب‌اپلیکیشن ASP.NET.

## اجرا

پیش‌نیاز: .NET 10 SDK و یک GitLab personal access token با دسترسی `read_api`.

```sh
dotnet build src/GitLabSearch -c Release
dotnet run --project src/GitLabSearch -- setup YOUR_TOKEN
dotnet run --project src/GitLabSearch -- "search term"
dotnet run --project src/GitLabSearch -- "search term" -g team/subgroup -e cs -p src -a exclude
```

## نصب به‌صورت ابزار

```sh
dotnet pack src/GitLabSearch -c Release -o artifacts
dotnet tool install --global --add-source ./artifacts GitLabSearch.Cli
gitlab-search --help
gitlab-search setup YOUR_TOKEN
gitlab-search "search term" --json
```

برای GitLab داخلی:

```sh
gitlab-search setup YOUR_TOKEN --api-domain https://gitlab.example.com --concurrency 10
gitlab-search setup YOUR_TOKEN --api-domain https://example.com/gitlab --dir /your/config/directory
```

`--ignore-ssl` اعتبارسنجی گواهی TLS را غیرفعال می‌کند؛ پیش‌فرض اعتبارسنجی فعال است.

## سازگاری و رفتار

- گزینه‌های `--groups / -g`، `--filename / -f`، `--extension / -e`، `--path / -p` و `--archive / -a` حفظ شده‌اند.
- `--archive` مقدار `all`، `only` یا `exclude` می‌گیرد؛ پیش‌فرض `all` است.
- گروه‌ها با شناسه یا مسیر کامل، جداشده با کاما، مشخص می‌شوند.
- مانند منبع اصلی، بدون `--groups` ابتدا گروه‌های قابل‌مشاهده خوانده می‌شوند و سپس پروژه‌های آن‌ها و زیرگروه‌هایشان با `include_subgroups=true` جست‌وجو می‌شوند؛ پروژه‌های شخصی خارج از گروه‌ها در این مسیر نیستند.
- فایل JSON قبلی `.gitlabsearchrc` با کلیدهای `token`, `domain`, `ignoreSSL`, `concurrency` قابل استفاده است. فایل در پوشه جاری، والدها و سپس home جست‌وجو می‌شود؛ نزدیک‌ترین فایل اولویت دارد. قالب INI و تمام مسیرهای بسته Node.js `rc` پیاده‌سازی نشده‌اند.
- `GITLABSEARCH_TOKEN` توکن فایل را جایگزین می‌کند و به‌تنهایی برای gitlab.com نیز کافی است.
- `--json` خروجی ساختاریافته می‌دهد. خروجی متنی لینک فایل و پیش‌نمایش را نمایش می‌دهد.
- همه صفحات گروه‌ها، پروژه‌ها و نتایج خوانده می‌شوند؛ پروژه‌های تکراری فقط یک‌بار جست‌وجو می‌شوند.
- محدودیت concurrency روی HTTP و HTTPS اعمال می‌شود. خطای هر درخواست کل اجرا را با کد 1 خاتمه می‌دهد؛ نتیجه ناقص به‌عنوان موفق چاپ نمی‌شود. Ctrl+C کد 130 دارد.
- `DEBUG=1` درخواست‌ها را در stderr ثبت می‌کند. توکن چاپ نمی‌شود.
- رنگ‌آمیزی regex نسخه اصلی حذف شده است؛ عبارت‌هایی مانند `a+b` به‌صورت معمول نمایش داده می‌شوند.
- قابلیت جست‌وجو و فیلترها تابع تنظیمات و نسخه GitLab هستند؛ برنامه با [Search API](https://docs.gitlab.com/api/search/) کار می‌کند.

## بررسی محلی

آزمون‌ها به GitLab واقعی یا توکن واقعی متصل نمی‌شوند. Postman CLI نسخه 1.70 به بالا و Python 3 برای بررسی HTTP لازم‌اند:

```sh
postman simulation run postman/simulations/gitlab-search.sim.yaml --port 4900 --output ndjson
# در ترمینال دوم:
dotnet build src/GitLabSearch -c Release
python3 tests/smoke.py
```

در صورت لزوم مسیر SDK را با `DOTNET=/absolute/path/to/dotnet` تنظیم کنید.
تست‌ها تنظیمات، مجوز فایل، صفحه‌بندی، حذف تکرار، فیلترها، UTF-8، archive، لینک‌ها، خطاهای 401/403 و جلوگیری از ارسال توکن به میزبان دیگر را بررسی می‌کنند. این بررسی جای تست روی GitLab واقعی را نمی‌گیرد.

## مجوز

MIT؛ متن و حق‌نشر پروژه اصلی در `LICENSE` حفظ شده است.
