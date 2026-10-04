# PTX Chrome Monitor

نرم‌افزار سبک Windows برای اندازه‌گیری مجموع مصرف اینترنت Google Chrome و هشدار قابل تنظیم.

## گرفتن فایل exe از GitHub

1. در GitHub یک Repository جدید بسازید (Private هم کافی است).
2. محتویات همین پوشه را داخل آن آپلود کنید. پوشه `.github` و پوشه `PTXChromeMonitor` حتماً باید باشند.
3. به تب **Actions** بروید. بعد از Push، workflow به نام **Build PTXChromeMonitor** خودکار اجرا می‌شود (حدود ۲ تا ۳ دقیقه).
4. روی اجرای تمام‌شده بزنید و پایین صفحه در بخش **Artifacts** فایل `PTXChromeMonitor` را دانلود کنید. داخل zip فایل `PTXChromeMonitor.exe` است.

اگر Tag به شکل `v1.0.0` بزنید، exe در بخش **Releases** هم قرار می‌گیرد:

```
git tag v1.0.0
git push --tags
```

## اجرا

- فایل exe مستقل است و نیازی به نصب .NET ندارد.
- برنامه **Administrator** می‌خواهد (برای ETW). UAC را تایید کنید.
- Windows SmartScreen ممکن است هشدار دهد چون فایل امضا نشده است: More info ← Run anyway.

## روش اندازه‌گیری

- ETW با provider کرنل `NetworkTCPIP`: بایت‌های TCP و UDP (IPv4 و IPv6) به تفکیک PID.
- فقط PIDهای `chrome.exe` جمع می‌شوند؛ ترافیک loopback شمرده نمی‌شود.
- واحدها مبنای ۱۰۲۴ هستند و ممکن است چند درصد با عدد ISP فرق کند.
- مصرف هر ۳۰ ثانیه و هنگام خروج در `%AppData%\PTXChromeMonitor\usage.json` ذخیره می‌شود.
- تنظیمات در `%AppData%\PTXChromeMonitor\settings.json` است.
- اجرای خودکار با Task Scheduler (بالاترین سطح دسترسی) ثبت می‌شود تا هر بار UAC نیاید.
- با تغییر دادن حد هشدار، هشدار دوباره مسلح می‌شود.

## تست‌ها (بند ۲۲ مشخصات)

1. دانلود یک فایل بزرگ در Chrome ← عدد مصرف را با Resource Monitor (تب Network) مقایسه کنید.
2. چند پنجره و چند Tab باز کنید ← Total مجموع همه است.
3. Chrome را ببندید ← سرعت صفر و پیام «Chrome در حال اجرا نیست».
4. حد را روی 500 MB بگذارید ← بعد از عبور، صدا و اعلان (طبق تیک‌ها).
5. هشدار تکرار نمی‌شود تا Reset بزنید.
6. Reset ← شمارنده صفر و هشدار دوباره فعال.
7. Auto Start را روشن کنید و Windows را Restart کنید.
