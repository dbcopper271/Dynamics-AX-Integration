# ABAP Programs – IT System Monitoring

Backend ABAP programs cung cấp dữ liệu cho IT Monitoring Tool (.NET).

## Cấu trúc

```
ABAPPrograms/
├── FunctionGroup/          # Function Group ZMON – RFC-enabled FMs
│   ├── ZMON_TOP.abap       # Global data declarations (INCLUDE TOP)
│   ├── Z_MON_GET_SYSTEM_HEALTH.abap
│   ├── Z_MON_GET_WORK_PROCESSES.abap
│   ├── Z_MON_GET_SHORT_DUMPS.abap
│   ├── Z_MON_GET_TRANSPORT_QUEUE.abap
│   ├── Z_MON_GET_BATCH_JOBS.abap
│   ├── Z_MON_GET_IDOC_STATUS.abap
│   └── Z_MON_GET_SYSLOG.abap
├── Reports/
│   └── Z_MON_DASHBOARD.abap   # ALV dashboard report
├── Includes/
│   └── Z_MON_TYPES.abap       # Shared types / structures
└── DataDictionary/
    └── ZMON_HEALTH_S.ddl      # Structure dictionary notes
```

## Cài đặt trong SAP

1. **Tạo Function Group** `ZMON` qua SE37 → Edit → Function Group
2. **Tạo các Function Module** RFC-enabled trong SE37
3. **Copy source code** từ mỗi file `.abap` vào FM tương ứng
4. **Activate** tất cả function modules
5. **Tạo Report** `Z_MON_DASHBOARD` qua SE38
6. **Cấp quyền** cho user `MONITOR_USER`:
   - S_RFC (RFC access)
   - S_BTCH_ADM (Batch job monitor)
   - S_TRANSPRT (Transport monitor)

## Kết nối từ .NET

Trong `SapRfcClient.cs`, thay stub bằng NCo calls tới các FM này.
