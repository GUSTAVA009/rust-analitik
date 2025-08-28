# 🚀 Быстрое обновление до версии 1.0.4

## ⚡ Срочное исправление ошибки компиляции

### 🐛 Если вы получили ошибку:
```
Unable to load SleepingBagLabels. A local or parameter named 'distance' cannot be declared...
```

### ✅ Решение:
**Обновите до версии 1.0.4** - ошибка полностью исправлена!

---

## 📋 Пошаговое обновление

### 1️⃣ Выгрузите старую версию
```bash
oxide.unload SleepingBagLabels
```

### 2️⃣ Замените файл плагина
- Скачайте новый `SleepingBagLabels.cs` (версия 1.0.4)
- Замените старый файл в папке `oxide/plugins/`

### 3️⃣ Загрузите новую версию
```bash
oxide.load SleepingBagLabels
```

### 4️⃣ Проверьте версию
```
/sleepingbag debug
```
Должно показать: `Plugin Version: 1.0.4`

---

## ✅ Что исправлено в версии 1.0.4

- **Ошибка компиляции** с конфликтом переменных
- **Переименована переменная** `distance` → `bagDistance` в debug команде
- **Плагин загружается** без ошибок

---

## 🎯 Быстрая проверка работоспособности

### После обновления выполните:
```
/sleepingbag status
```

### Должно показать:
```
📊 Your Sleeping Bag Labels Settings:
   Labels: ✅ ENABLED
   Max Distance: 50 meters
   Global Settings: ✅ ON
```

### Если метки не видны:
```
/sleepingbag toggle
```

---

## 🆘 Если проблемы остались

### 1. Проверьте права доступа:
```bash
oxide.grant group default sleepingbaglabels.use
```

### 2. Принудительно обновите метки:
```
/sleepingbag refresh
```

### 3. Проверьте отладочную информацию:
```
/sleepingbag debug
```

---

<div align="center">

**🎉 Версия 1.0.4 полностью исправляет ошибку компиляции!**

*Все функции работают корректно*

</div>