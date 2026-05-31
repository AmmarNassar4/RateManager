# RateManager.Net10

مشروع ASP.NET Core MVC مستهدف `.NET 10` لإدارة أسعار الغرف بدل ملف Excel.

## المطلوب على الجهاز

- Visual Studio يدعم .NET 10
- .NET 10 SDK
- SQL Server أو SQL Server Express

## التشغيل السريع

1. افتح الملف:

   `RateManager.Net10.sln`

2. عدل الاتصال بقاعدة البيانات من:

   `RateManager.Net10/appsettings.json`

   مثال:

   ```json
   "DefaultConnection": "Server=.;Database=RateManagerDb;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true"
   ```

3. شغل المشروع من Visual Studio.

المشروع مضبوط على:

```json
"AutoCreateDatabase": true
```

يعني سيحاول إنشاء الجداول تلقائيًا عند أول تشغيل. لو تريد إنشاء القاعدة يدويًا استخدم:

`database/create_database.sql`

## الشاشات الموجودة

- الرئيسية: تعرض آخر دفعات الحساب أو الاستيراد.
- حساب الأسعار: تختار عدد الأيام، نوع الغرفة، عدد النزلاء لكل غرفة، عدد الغرف، السعر الأساسي، ونسبة الزيادة أو النقصان.
- استيراد Excel: تدخل مسار ملف Excel ليقرأ الأسعار بدل الحساب.
- عرض الأسعار: جدول ديناميكي لكل الأيام والغرف، مع إمكانية تعديل أي سعر يدويًا.
- الإعدادات: إضافة أنواع غرف وخطط أسعار بأسماء واضحة.

## منطق الحساب

```text
CalculatedRate = BaseRate × (1 + TotalAdjustmentPercent / 100)
FinalRate = ManualRate إن وجدت، وإلا CalculatedRate
```

أي تعديل يدوي يتسجل في:

- `RateOverrides`
- `RateAuditLogs`

## استيراد Excel

الاستيراد يدعم شكلين:

1. جدول عادي فيه أعمدة مثل:

```text
Date | RoomType | Rate | GuestCount | RoomCount
```

2. Matrix:

```text
Room Type | 2026-06-01 | 2026-06-02 | 2026-06-03
Standard King | 450 | 460 | 470
Standard Twin | 430 | 440 | 450
```

لو الشيت لا يحتوي تواريخ واضحة، النظام يستخدم `StartDate` وعدد الأيام من شاشة الاستيراد.

## ملاحظة مهمة

قاعدة البيانات تستخدم أسماء واضحة مستقلة عن أسماء النظام القديم. الربط مع PMS أو الجداول القديمة سيتم لاحقًا في طبقة Mapping منفصلة.
