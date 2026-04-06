*&---------------------------------------------------------------------*
*& Report  Z_MON_DASHBOARD
*& IT System Monitoring Dashboard – ALV Grid with color-coded status
*&---------------------------------------------------------------------*
*
* Description: Aggregates health data from all Z_MON_GET_* function
*              modules and displays them in a unified ALV dashboard.
*              Run via SE38 or assign to a transaction code Z_MON.
*
* Selection screen:
*   - Window minutes   (default 60)
*   - Auto-refresh     (default 60 seconds)
*   - Filter by system
*   - Include IDoc monitoring (optional, may be slow)
*
* Features:
*   - Color-coded status (Green/Yellow/Red)
*   - Traffic light symbols
*   - Drill-down to detail for each category
*   - Auto-refresh via SET RUN TIME CLOCK / timer approach
*
* Authorization:
*   S_RFC, S_BTCH_ADM, S_TRANSPRT, S_ADMI_FCD-SLGR
*---------------------------------------------------------------------*

REPORT z_mon_dashboard.

INCLUDE z_mon_types.

"──────────────────────────────────────────────────────────────────────
" Selection Screen
"──────────────────────────────────────────────────────────────────────
SELECTION-SCREEN BEGIN OF BLOCK b1 WITH FRAME TITLE TEXT-b01.
  PARAMETERS:
    p_winmin TYPE i DEFAULT 60        OBLIGATORY,  " Window (minutes)
    p_refr   TYPE i DEFAULT 60,                    " Auto-refresh (sec, 0=off)
    p_maxrow TYPE i DEFAULT 200.                   " Max rows per section
SELECTION-SCREEN END OF BLOCK b1.

SELECTION-SCREEN BEGIN OF BLOCK b2 WITH FRAME TITLE TEXT-b02.
  PARAMETERS:
    p_idoc   AS CHECKBOX DEFAULT 'X', " Monitor IDocs
    p_syslog AS CHECKBOX DEFAULT 'X', " Monitor Syslog
    p_transp AS CHECKBOX DEFAULT 'X', " Monitor Transports
    p_batch  AS CHECKBOX DEFAULT 'X'. " Monitor Batch Jobs
SELECTION-SCREEN END OF BLOCK b2.

"──────────────────────────────────────────────────────────────────────
" Types
"──────────────────────────────────────────────────────────────────────
TYPES: BEGIN OF ty_dashboard_row,
         category      TYPE c LENGTH 25,
         check_name    TYPE c LENGTH 35,
         status        TYPE c LENGTH 10,
         status_icon   TYPE c LENGTH 4,   " ✔ / ⚠ / ✖
         message       TYPE c LENGTH 200,
         checked_at    TYPE timestamp,
         color_code    TYPE c LENGTH 4,   " ALV color key
       END OF ty_dashboard_row.

DATA: gt_dashboard   TYPE STANDARD TABLE OF ty_dashboard_row,
      gs_row         TYPE ty_dashboard_row.

"──────────────────────────────────────────────────────────────────────
" ALV field catalog
"──────────────────────────────────────────────────────────────────────
DATA: gt_fieldcat    TYPE slis_t_fieldcat_alv,
      gs_layout      TYPE slis_layout_alv,
      gt_sort        TYPE slis_t_sortinfo_alv,
      gt_events      TYPE slis_t_event.

"──────────────────────────────────────────────────────────────────────
" START-OF-SELECTION
"──────────────────────────────────────────────────────────────────────
START-OF-SELECTION.
  PERFORM collect_all_data.
  PERFORM build_fieldcat.
  PERFORM build_layout.
  PERFORM display_alv.

"──────────────────────────────────────────────────────────────────────
" FORM collect_all_data
"──────────────────────────────────────────────────────────────────────
FORM collect_all_data.

  CLEAR gt_dashboard.

* ── 1. System Health ──────────────────────────────────────────────────
  PERFORM check_system_health.

* ── 2. Work Processes ────────────────────────────────────────────────
  PERFORM check_work_processes.

* ── 3. Short Dumps ────────────────────────────────────────────────────
  PERFORM check_short_dumps.

* ── 4. Transport Queue ────────────────────────────────────────────────
  IF p_transp = 'X'.
    PERFORM check_transports.
  ENDIF.

* ── 5. Batch Jobs ─────────────────────────────────────────────────────
  IF p_batch = 'X'.
    PERFORM check_batch_jobs.
  ENDIF.

* ── 6. IDoc Status ────────────────────────────────────────────────────
  IF p_idoc = 'X'.
    PERFORM check_idoc_status.
  ENDIF.

* ── 7. System Log ─────────────────────────────────────────────────────
  IF p_syslog = 'X'.
    PERFORM check_syslog.
  ENDIF.

ENDFORM.

"──────────────────────────────────────────────────────────────────────
" FORM check_system_health
"──────────────────────────────────────────────────────────────────────
FORM check_system_health.
  DATA: ls_health  TYPE ty_system_health,
        lt_metrics TYPE ty_metrics,
        lv_status  TYPE c LENGTH 10,
        lv_message TYPE string.

  CALL FUNCTION 'Z_MON_GET_SYSTEM_HEALTH'
    IMPORTING
      es_health  = ls_health
      et_metrics = lt_metrics
      ev_status  = lv_status
      ev_message = lv_message
    EXCEPTIONS
      OTHERS     = 1.

  IF sy-subrc <> 0.
    lv_status  = gc_unhealthy.
    lv_message = 'Z_MON_GET_SYSTEM_HEALTH failed'.
  ENDIF.

  CLEAR gs_row.
  gs_row-category    = 'SAP System'.
  gs_row-check_name  = |System Health ({ sy-sysid })|.
  gs_row-message     = lv_message.
  GET TIME STAMP FIELD gs_row-checked_at.
  PERFORM set_status_fields USING lv_status CHANGING gs_row.
  APPEND gs_row TO gt_dashboard.

ENDFORM.

"──────────────────────────────────────────────────────────────────────
" FORM check_work_processes
"──────────────────────────────────────────────────────────────────────
FORM check_work_processes.
  DATA: lt_wp      TYPE ty_work_processes,
        lv_total   TYPE i,
        lv_free    TYPE i,
        lv_pct     TYPE p DECIMALS 2,
        lv_status  TYPE c LENGTH 10,
        lv_message TYPE string.

  CALL FUNCTION 'Z_MON_GET_WORK_PROCESSES'
    IMPORTING
      ev_total_wp     = lv_total
      ev_free_wp      = lv_free
      ev_free_dia_pct = lv_pct
      ev_status       = lv_status
      ev_message      = lv_message
    TABLES
      et_work_processes = lt_wp
    EXCEPTIONS
      OTHERS = 1.

  IF sy-subrc <> 0.
    lv_status  = gc_unhealthy.
    lv_message = 'Z_MON_GET_WORK_PROCESSES failed'.
  ENDIF.

  CLEAR gs_row.
  gs_row-category   = 'SAP System'.
  gs_row-check_name = |Work Processes (Free: { lv_pct }%)|.
  gs_row-message    = lv_message.
  GET TIME STAMP FIELD gs_row-checked_at.
  PERFORM set_status_fields USING lv_status CHANGING gs_row.
  APPEND gs_row TO gt_dashboard.

ENDFORM.

"──────────────────────────────────────────────────────────────────────
" FORM check_short_dumps
"──────────────────────────────────────────────────────────────────────
FORM check_short_dumps.
  DATA: lt_dumps   TYPE ty_short_dumps,
        lv_count   TYPE i,
        lv_status  TYPE c LENGTH 10,
        lv_message TYPE string.

  CALL FUNCTION 'Z_MON_GET_SHORT_DUMPS'
    EXPORTING
      iv_window_minutes = p_winmin
      iv_max_rows       = p_maxrow
    IMPORTING
      ev_dump_count     = lv_count
      ev_status         = lv_status
      ev_message        = lv_message
    TABLES
      et_short_dumps    = lt_dumps
    EXCEPTIONS
      OTHERS = 1.

  IF sy-subrc <> 0.
    lv_status  = gc_unhealthy.
    lv_message = 'Z_MON_GET_SHORT_DUMPS failed'.
  ENDIF.

  CLEAR gs_row.
  gs_row-category   = 'SAP System'.
  gs_row-check_name = |ABAP Short Dumps (last { p_winmin } min)|.
  gs_row-message    = lv_message.
  GET TIME STAMP FIELD gs_row-checked_at.
  PERFORM set_status_fields USING lv_status CHANGING gs_row.
  APPEND gs_row TO gt_dashboard.

ENDFORM.

"──────────────────────────────────────────────────────────────────────
" FORM check_transports
"──────────────────────────────────────────────────────────────────────
FORM check_transports.
  DATA: lt_tp      TYPE ty_transports,
        lv_depth   TYPE i,
        lv_status  TYPE c LENGTH 10,
        lv_message TYPE string.

  CALL FUNCTION 'Z_MON_GET_TRANSPORT_QUEUE'
    EXPORTING
      iv_max_rows    = p_maxrow
    IMPORTING
      ev_queue_depth = lv_depth
      ev_status      = lv_status
      ev_message     = lv_message
    TABLES
      et_transports  = lt_tp
    EXCEPTIONS
      OTHERS = 1.

  IF sy-subrc <> 0.
    lv_status  = gc_unhealthy.
    lv_message = 'Z_MON_GET_TRANSPORT_QUEUE failed'.
  ENDIF.

  CLEAR gs_row.
  gs_row-category   = 'SAP CTS'.
  gs_row-check_name = |Transport Queue (Depth: { lv_depth })|.
  gs_row-message    = lv_message.
  GET TIME STAMP FIELD gs_row-checked_at.
  PERFORM set_status_fields USING lv_status CHANGING gs_row.
  APPEND gs_row TO gt_dashboard.

ENDFORM.

"──────────────────────────────────────────────────────────────────────
" FORM check_batch_jobs
"──────────────────────────────────────────────────────────────────────
FORM check_batch_jobs.
  DATA: lt_jobs    TYPE ty_batch_jobs,
        lv_aborted TYPE i,
        lv_active  TYPE i,
        lv_status  TYPE c LENGTH 10,
        lv_message TYPE string.

  CALL FUNCTION 'Z_MON_GET_BATCH_JOBS'
    EXPORTING
      iv_window_minutes = p_winmin
    IMPORTING
      ev_aborted_count  = lv_aborted
      ev_active_count   = lv_active
      ev_status         = lv_status
      ev_message        = lv_message
    TABLES
      et_jobs           = lt_jobs
    EXCEPTIONS
      OTHERS = 1.

  IF sy-subrc <> 0.
    lv_status  = gc_unhealthy.
    lv_message = 'Z_MON_GET_BATCH_JOBS failed'.
  ENDIF.

  CLEAR gs_row.
  gs_row-category   = 'SAP Batch'.
  gs_row-check_name = |Batch Jobs (Active:{ lv_active } Cancel:{ lv_aborted })|.
  gs_row-message    = lv_message.
  GET TIME STAMP FIELD gs_row-checked_at.
  PERFORM set_status_fields USING lv_status CHANGING gs_row.
  APPEND gs_row TO gt_dashboard.

ENDFORM.

"──────────────────────────────────────────────────────────────────────
" FORM check_idoc_status
"──────────────────────────────────────────────────────────────────────
FORM check_idoc_status.
  DATA: lt_errs    TYPE ty_idoc_errors,
        lv_total   TYPE i,
        lv_errors  TYPE i,
        lv_success TYPE i,
        lv_rate    TYPE p DECIMALS 2,
        lv_status  TYPE c LENGTH 10,
        lv_message TYPE string.

  CALL FUNCTION 'Z_MON_GET_IDOC_STATUS'
    EXPORTING
      iv_window_minutes = p_winmin
      iv_max_rows       = p_maxrow
    IMPORTING
      ev_total_idocs    = lv_total
      ev_error_count    = lv_errors
      ev_success_count  = lv_success
      ev_error_rate_pct = lv_rate
      ev_status         = lv_status
      ev_message        = lv_message
    TABLES
      et_idoc_errors    = lt_errs
    EXCEPTIONS
      OTHERS = 1.

  IF sy-subrc <> 0.
    lv_status  = gc_unhealthy.
    lv_message = 'Z_MON_GET_IDOC_STATUS failed'.
  ENDIF.

  CLEAR gs_row.
  gs_row-category   = 'SAP IDoc/ALE'.
  gs_row-check_name = |IDocs (Total:{ lv_total } Err:{ lv_errors } { lv_rate }%)|.
  gs_row-message    = lv_message.
  GET TIME STAMP FIELD gs_row-checked_at.
  PERFORM set_status_fields USING lv_status CHANGING gs_row.
  APPEND gs_row TO gt_dashboard.

ENDFORM.

"──────────────────────────────────────────────────────────────────────
" FORM check_syslog
"──────────────────────────────────────────────────────────────────────
FORM check_syslog.
  DATA: lt_sl        TYPE ty_syslog_entries,
        lv_aborts    TYPE i,
        lv_errors    TYPE i,
        lv_warns     TYPE i,
        lv_status    TYPE c LENGTH 10,
        lv_message   TYPE string.

  CALL FUNCTION 'Z_MON_GET_SYSLOG'
    EXPORTING
      iv_window_minutes  = p_winmin
      iv_min_class       = 'W'
      iv_max_rows        = p_maxrow
    IMPORTING
      ev_abort_count     = lv_aborts
      ev_error_count     = lv_errors
      ev_warn_count      = lv_warns
      ev_status          = lv_status
      ev_message         = lv_message
    TABLES
      et_syslog_entries  = lt_sl
    EXCEPTIONS
      OTHERS = 1.

  IF sy-subrc <> 0.
    lv_status  = gc_unhealthy.
    lv_message = 'Z_MON_GET_SYSLOG failed'.
  ENDIF.

  CLEAR gs_row.
  gs_row-category   = 'SAP System'.
  gs_row-check_name = |System Log (A:{ lv_aborts } E:{ lv_errors } W:{ lv_warns })|.
  gs_row-message    = lv_message.
  GET TIME STAMP FIELD gs_row-checked_at.
  PERFORM set_status_fields USING lv_status CHANGING gs_row.
  APPEND gs_row TO gt_dashboard.

ENDFORM.

"──────────────────────────────────────────────────────────────────────
" FORM set_status_fields  – Map status string to icon + ALV color
"──────────────────────────────────────────────────────────────────────
FORM set_status_fields
  USING    iv_status  TYPE c
  CHANGING cs_row     TYPE ty_dashboard_row.

  cs_row-status = iv_status.

  CASE iv_status.
    WHEN gc_healthy.
      cs_row-status_icon = icon_green_light.
      cs_row-color_code  = 'C510'.   " ALV: green row highlight
    WHEN gc_degraded.
      cs_row-status_icon = icon_yellow_light.
      cs_row-color_code  = 'C310'.   " ALV: yellow row highlight
    WHEN gc_unhealthy.
      cs_row-status_icon = icon_red_light.
      cs_row-color_code  = 'C610'.   " ALV: red row highlight
    WHEN OTHERS.
      cs_row-status_icon = icon_message_information.
      cs_row-color_code  = 'C110'.   " ALV: grey
  ENDCASE.

ENDFORM.

"──────────────────────────────────────────────────────────────────────
" FORM build_fieldcat
"──────────────────────────────────────────────────────────────────────
FORM build_fieldcat.
  DATA: ls_fc TYPE slis_fieldcat_alv.

  DEFINE add_field.
    CLEAR ls_fc.
    ls_fc-fieldname  = &1.
    ls_fc-seltext_m  = &2.
    ls_fc-outputlen  = &3.
    ls_fc-just       = &4.   " L=Left, R=Right, C=Center
    APPEND ls_fc TO gt_fieldcat.
  END-OF-DEFINITION.

  add_field 'STATUS_ICON' 'St'  4   'C'.
  add_field 'STATUS'      'Status'  12  'L'.
  add_field 'CATEGORY'    'Category' 25 'L'.
  add_field 'CHECK_NAME'  'Check'  35  'L'.
  add_field 'MESSAGE'     'Message'  70 'L'.
  add_field 'CHECKED_AT'  'Checked (UTC)' 20 'C'.

ENDFORM.

"──────────────────────────────────────────────────────────────────────
" FORM build_layout
"──────────────────────────────────────────────────────────────────────
FORM build_layout.
  gs_layout-zebra            = ' '.
  gs_layout-colwidth_optimize = 'X'.
  gs_layout-info_fieldname   = 'COLOR_CODE'.   " Row color from field
  gs_layout-window_titlebar  = 'IT Monitoring Dashboard'.
  gs_layout-detail_popup     = 'X'.
ENDFORM.

"──────────────────────────────────────────────────────────────────────
" FORM display_alv
"──────────────────────────────────────────────────────────────────────
FORM display_alv.

  " Sort: unhealthy first, then degraded, then healthy
  DATA: ls_sort TYPE slis_sortinfo_alv.
  ls_sort-fieldname = 'STATUS'.
  ls_sort-spos      = 1.
  ls_sort-up        = ' '.  " Descending sort (U=unhealthy first alphabetically)
  APPEND ls_sort TO gt_sort.

  ls_sort-fieldname = 'CATEGORY'.
  ls_sort-spos      = 2.
  ls_sort-up        = 'X'.
  APPEND ls_sort TO gt_sort.

* Top header
  DATA: lt_list_header TYPE slis_t_listheader.
  DATA: ls_hdr         TYPE slis_listheader.

  ls_hdr-typ  = 'H'.
  ls_hdr-info = |IT System Monitoring Dashboard – { sy-sysid } | &&
                |{ sy-datum } { sy-uzeit }|.
  APPEND ls_hdr TO lt_list_header.

  ls_hdr-typ  = 'S'.
  ls_hdr-key  = 'Window:'.
  ls_hdr-info = |Last { p_winmin } minutes|.
  APPEND ls_hdr TO lt_list_header.

  DATA(lv_healthy)   = lines( FILTER #( gt_dashboard WHERE status = gc_healthy ) ).
  DATA(lv_degraded)  = lines( FILTER #( gt_dashboard WHERE status = gc_degraded ) ).
  DATA(lv_unhealthy) = lines( FILTER #( gt_dashboard WHERE status = gc_unhealthy ) ).

  ls_hdr-typ  = 'A'.
  ls_hdr-info = |Healthy: { lv_healthy }  Degraded: { lv_degraded } |
             && | Unhealthy: { lv_unhealthy }|.
  APPEND ls_hdr TO lt_list_header.

  CALL FUNCTION 'REUSE_ALV_GRID_DISPLAY'
    EXPORTING
      it_fieldcat        = gt_fieldcat
      is_layout          = gs_layout
      it_sort            = gt_sort
      it_list_header     = lt_list_header
      i_callback_program = sy-repid
      i_save             = 'A'
    TABLES
      t_outtab           = gt_dashboard
    EXCEPTIONS
      program_error      = 1
      OTHERS             = 2.

  IF sy-subrc <> 0.
    MESSAGE 'ALV display error' TYPE 'E'.
  ENDIF.

ENDFORM.
