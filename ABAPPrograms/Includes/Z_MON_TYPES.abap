*&---------------------------------------------------------------------*
*& Include Z_MON_TYPES
*& Shared type definitions for IT Monitoring Function Group ZMON
*&---------------------------------------------------------------------*

"──────────────────────────────────────────────────────────────────────
" System Health Summary
"──────────────────────────────────────────────────────────────────────
TYPES: BEGIN OF ty_system_health,
         sid           TYPE sysysid,      " System ID (e.g. PRD)
         host          TYPE msxxlist-host, " Application server hostname
         sap_release   TYPE sy-saprl,     " SAP Release (e.g. 756)
         kernel_rel    TYPE c LENGTH 20,  " Kernel release
         db_type       TYPE dbsys,        " Database type
         db_host       TYPE c LENGTH 60,  " Database hostname
         db_name       TYPE c LENGTH 60,  " Database name (SID/schema)
         inst_number   TYPE c LENGTH 2,   " Instance number
         start_time    TYPE timestamp,    " System start timestamp
         timezone      TYPE timezone,     " System timezone
         codepage      TYPE cpcodepage,   " Code page
         status        TYPE c LENGTH 10,  " HEALTHY/DEGRADED/UNHEALTHY
       END OF ty_system_health.

"──────────────────────────────────────────────────────────────────────
" Work Process
"──────────────────────────────────────────────────────────────────────
TYPES: BEGIN OF ty_work_process,
         wp_no         TYPE i,            " Work process number
         wp_type       TYPE c LENGTH 2,   " DIA/BTC/SPO/UPD/ENQ/...
         wp_status     TYPE c LENGTH 10,  " Wait/Run/Hold/...
         wp_pid        TYPE i,            " OS Process ID
         wp_program    TYPE c LENGTH 40,  " Currently running program
         wp_user       TYPE uname,        " Current logon user
         wp_client     TYPE mandt,        " Client
         wp_cpu_time   TYPE i,            " CPU time (ms)
         wp_elapsed    TYPE i,            " Elapsed time (ms)
         wp_semaphore  TYPE i,            " Semaphore hold (-1 = none)
       END OF ty_work_process.

TYPES ty_work_processes TYPE STANDARD TABLE OF ty_work_process
        WITH DEFAULT KEY.

"──────────────────────────────────────────────────────────────────────
" Short Dump (ABAP Runtime Error)
"──────────────────────────────────────────────────────────────────────
TYPES: BEGIN OF ty_short_dump,
         dump_id       TYPE snaptime,     " Dump timestamp key
         mandt         TYPE mandt,
         username      TYPE uname,
         progname      TYPE progname,     " ABAP program name
         errtyp        TYPE c LENGTH 40,  " Error type (e.g. SYSTEM_FAILURE)
         errmsg        TYPE c LENGTH 200, " Short error message
         occurred_at   TYPE timestamp,    " When it occurred (UTC)
       END OF ty_short_dump.

TYPES ty_short_dumps TYPE STANDARD TABLE OF ty_short_dump
        WITH DEFAULT KEY.

"──────────────────────────────────────────────────────────────────────
" Transport Queue Entry
"──────────────────────────────────────────────────────────────────────
TYPES: BEGIN OF ty_transport,
         trkorr        TYPE trkorr,       " Transport request number
         as4text       TYPE as4text,      " Description
         trstatus      TYPE trstatus,     " Status: D/L/R/...
         trfunction    TYPE trfunction,   " K=Workbench/W=Customizing
         as4user       TYPE as4user,      " Owner
         as4date       TYPE as4date,      " Creation date
         target_sys    TYPE c LENGTH 10,  " Target system SID
         import_date   TYPE d,            " Scheduled import date
         import_time   TYPE t,
       END OF ty_transport.

TYPES ty_transports TYPE STANDARD TABLE OF ty_transport
        WITH DEFAULT KEY.

"──────────────────────────────────────────────────────────────────────
" Batch Job
"──────────────────────────────────────────────────────────────────────
TYPES: BEGIN OF ty_batch_job,
         jobname       TYPE btcjob,       " Job name
         jobcount      TYPE btcjobcnt,    " Job count (unique key)
         status        TYPE btcstatus,    " S=Scheduled/A=Active/F=Finished/X=Cancelled
         sdlstrtdt     TYPE btcsdldate,   " Scheduled start date
         sdlstrttm     TYPE btcsdltime,   " Scheduled start time
         strtdate      TYPE btcjobdate,   " Actual start date
         strttime      TYPE btcjobtime,   " Actual start time
         enddate       TYPE btcjobdate,   " End date
         endtime       TYPE btcjobtime,   " End time
         username      TYPE btcuname,     " Owner
         duration_s    TYPE i,            " Duration in seconds
         aborted_at    TYPE timestamp,    " If cancelled, when
       END OF ty_batch_job.

TYPES ty_batch_jobs TYPE STANDARD TABLE OF ty_batch_job
        WITH DEFAULT KEY.

"──────────────────────────────────────────────────────────────────────
" IDoc Status
"──────────────────────────────────────────────────────────────────────
TYPES: BEGIN OF ty_idoc_error,
         docnum        TYPE edidc-docnum, " IDoc number
         mestyp        TYPE edidc-mestyp, " Message type
         mescod        TYPE edidc-mescod,
         mesfct        TYPE edidc-mesfct,
         direction     TYPE edidc-direct, " 1=Outbound/2=Inbound
         status        TYPE edids-status, " Status code
         statustext    TYPE c LENGTH 100, " Status description
         sndprt        TYPE edidc-sndprt, " Sender partner type
         sndprn        TYPE edidc-sndprn, " Sender partner number
         rcvprt        TYPE edidc-rcvprt, " Receiver partner type
         rcvprn        TYPE edidc-rcvprn, " Receiver partner number
         credat        TYPE edidc-credat, " Creation date
         cretim        TYPE edidc-cretim, " Creation time
         error_msg     TYPE c LENGTH 200, " Error message text
       END OF ty_idoc_error.

TYPES ty_idoc_errors TYPE STANDARD TABLE OF ty_idoc_error
        WITH DEFAULT KEY.

"──────────────────────────────────────────────────────────────────────
" System Log Entry
"──────────────────────────────────────────────────────────────────────
TYPES: BEGIN OF ty_syslog_entry,
         log_time      TYPE timestamp,    " UTC timestamp of entry
         syslog_class  TYPE c LENGTH 1,   " A=Abort/E=Error/W=Warning/I=Info
         msg_id        TYPE c LENGTH 3,   " Message ID
         msg_text      TYPE c LENGTH 200, " Message text
         terminal      TYPE c LENGTH 20,  " Terminal/server
         user_name     TYPE uname,
         trans_code    TYPE tcode,
       END OF ty_syslog_entry.

TYPES ty_syslog_entries TYPE STANDARD TABLE OF ty_syslog_entry
        WITH DEFAULT KEY.

"──────────────────────────────────────────────────────────────────────
" Generic metric for extensible key-value reporting
"──────────────────────────────────────────────────────────────────────
TYPES: BEGIN OF ty_metric,
         metric_key    TYPE c LENGTH 50,
         metric_value  TYPE c LENGTH 100,
         metric_unit   TYPE c LENGTH 20,
       END OF ty_metric.

TYPES ty_metrics TYPE STANDARD TABLE OF ty_metric
        WITH DEFAULT KEY.
