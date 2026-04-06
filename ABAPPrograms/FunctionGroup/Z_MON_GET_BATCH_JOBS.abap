FUNCTION Z_MON_GET_BATCH_JOBS.
*"----------------------------------------------------------------------
*"*"Local Interface:
*"  IMPORTING
*"     VALUE(IV_JOB_NAME)       TYPE  BTCJOB    OPTIONAL "(wildcard: 'Z_*')
*"     VALUE(IV_WINDOW_MINUTES) TYPE  I         DEFAULT 60
*"     VALUE(IV_STATUS_FILTER)  TYPE  C         OPTIONAL "(A/F/X/S/blank=all)
*"  EXPORTING
*"     VALUE(EV_ABORTED_COUNT)  TYPE  I
*"     VALUE(EV_ACTIVE_COUNT)   TYPE  I
*"     VALUE(EV_STATUS)         TYPE  C
*"     VALUE(EV_MESSAGE)        TYPE  STRING
*"  TABLES
*"     ET_JOBS                  TYPE  TY_BATCH_JOBS
*"  EXCEPTIONS
*"     SYSTEM_ERROR             1
*"----------------------------------------------------------------------
*
* RFC-enabled: YES
* Description: Returns batch job status from TBTCO / TBTCS tables.
*
* Job status codes (BTCSTATUS):
*   S = Scheduled     A = Active    F = Finished
*   X = Cancelled     Y = Waiting   R = Released
*   Z = Obsolete
*
* Key tables:
*   TBTCO  – Job overview (actual run info)
*   TBTCS  – Job steps (program/variant)
*
* Authorization: S_BTCH_ADM (batch administration)
*----------------------------------------------------------------------

  CONSTANTS:
    lc_warn_aborted TYPE i VALUE 1,
    lc_crit_aborted TYPE i VALUE 5.

  DATA: ls_job    TYPE ty_batch_job,
        lv_from_dt TYPE d,
        lv_from_tm TYPE t.

  CLEAR: et_jobs, ev_aborted_count, ev_active_count, ev_status, ev_message.

  DATA(lv_minutes) = COND #( WHEN iv_window_minutes > 0
                             THEN iv_window_minutes ELSE 60 ).

  TRY.
*   ── Calculate start-of-window ────────────────────────────────────────
    GET TIME STAMP FIELD DATA(lv_now_ts).
    DATA(lv_from_ts) = lv_now_ts - ( lv_minutes * 60 ).
    CONVERT TIME STAMP lv_from_ts TIME ZONE 'UTC'
      INTO DATE lv_from_dt TIME lv_from_tm.

*   ── Select jobs from TBTCO ───────────────────────────────────────────
*   TBTCO fields: jobname, jobcount, status, sdlstrtdt, sdlstrttm,
*                 strtdate, strttime, enddate, endtime, authckman
    IF iv_job_name IS NOT INITIAL AND iv_status_filter IS NOT INITIAL.
      SELECT jobname, jobcount, status, sdlstrtdt, sdlstrttm,
             strtdate, strttime, enddate, endtime, authckman AS username
        FROM tbtco
        INTO TABLE @DATA(lt_tbtco)
        WHERE jobname  LIKE @iv_job_name
          AND status    = @iv_status_filter
          AND ( strtdate >= @lv_from_dt
             OR sdlstrtdt >= @lv_from_dt )
        ORDER BY strtdate DESCENDING, strttime DESCENDING.      "#EC CI_NOWHERE

    ELSEIF iv_job_name IS NOT INITIAL.
      SELECT jobname, jobcount, status, sdlstrtdt, sdlstrttm,
             strtdate, strttime, enddate, endtime, authckman AS username
        FROM tbtco
        INTO TABLE @lt_tbtco
        WHERE jobname LIKE @iv_job_name
          AND ( strtdate >= @lv_from_dt
             OR sdlstrtdt >= @lv_from_dt )
        ORDER BY strtdate DESCENDING, strttime DESCENDING.      "#EC CI_NOWHERE

    ELSEIF iv_status_filter IS NOT INITIAL.
      SELECT jobname, jobcount, status, sdlstrtdt, sdlstrttm,
             strtdate, strttime, enddate, endtime, authckman AS username
        FROM tbtco
        INTO TABLE @lt_tbtco
        WHERE status = @iv_status_filter
          AND ( strtdate >= @lv_from_dt
             OR sdlstrtdt >= @lv_from_dt )
        ORDER BY strtdate DESCENDING, strttime DESCENDING.      "#EC CI_NOWHERE

    ELSE.
      SELECT jobname, jobcount, status, sdlstrtdt, sdlstrttm,
             strtdate, strttime, enddate, endtime, authckman AS username
        FROM tbtco
        INTO TABLE @lt_tbtco
        WHERE ( strtdate >= @lv_from_dt
             OR sdlstrtdt >= @lv_from_dt )
        ORDER BY strtdate DESCENDING, strttime DESCENDING.      "#EC CI_NOWHERE
    ENDIF.

*   ── Map to monitoring structure ──────────────────────────────────────
    LOOP AT lt_tbtco INTO DATA(ls_tbtco).
      CLEAR ls_job.
      ls_job-jobname   = ls_tbtco-jobname.
      ls_job-jobcount  = ls_tbtco-jobcount.
      ls_job-status    = ls_tbtco-status.
      ls_job-sdlstrtdt = ls_tbtco-sdlstrtdt.
      ls_job-sdlstrttm = ls_tbtco-sdlstrttm.
      ls_job-strtdate  = ls_tbtco-strtdate.
      ls_job-strttime  = ls_tbtco-strttime.
      ls_job-enddate   = ls_tbtco-enddate.
      ls_job-endtime   = ls_tbtco-endtime.
      ls_job-username  = ls_tbtco-username.

*     Calculate duration in seconds
      IF ls_tbtco-strtdate IS NOT INITIAL AND ls_tbtco-enddate IS NOT INITIAL.
        DATA(lv_start_ts) TYPE timestamp.
        DATA(lv_end_ts)   TYPE timestamp.
        CONVERT DATE ls_tbtco-strtdate TIME ls_tbtco-strttime
          TIME ZONE 'UTC' INTO TIME STAMP lv_start_ts TIME ZONE 'UTC'.
        CONVERT DATE ls_tbtco-enddate TIME ls_tbtco-endtime
          TIME ZONE 'UTC' INTO TIME STAMP lv_end_ts TIME ZONE 'UTC'.
        ls_job-duration_s = lv_end_ts - lv_start_ts.
      ENDIF.

*     Capture abort timestamp
      IF ls_tbtco-status = 'X'.
        CONVERT DATE ls_tbtco-enddate TIME ls_tbtco-endtime
          TIME ZONE 'UTC' INTO TIME STAMP ls_job-aborted_at TIME ZONE 'UTC'.
        ev_aborted_count = ev_aborted_count + 1.
      ENDIF.

      IF ls_tbtco-status = 'A'.
        ev_active_count = ev_active_count + 1.
      ENDIF.

      APPEND ls_job TO et_jobs.
    ENDLOOP.

*   ── Determine health status ──────────────────────────────────────────
    IF ev_aborted_count >= lc_crit_aborted.
      ev_status  = gc_unhealthy.
      ev_message = |{ ev_aborted_count } batch job(s) CANCELLED in last |
                && |{ lv_minutes } min (critical: { lc_crit_aborted })|.
    ELSEIF ev_aborted_count >= lc_warn_aborted.
      ev_status  = gc_degraded.
      ev_message = |{ ev_aborted_count } batch job(s) CANCELLED in last |
                && |{ lv_minutes } min|.
    ELSE.
      ev_status  = gc_healthy.
      ev_message = |Batch jobs OK: { ev_active_count } active, |
                && |{ ev_aborted_count } cancelled in last { lv_minutes } min|.
    ENDIF.

  CATCH cx_root INTO DATA(lx).
    ev_status  = gc_unhealthy.
    ev_message = lx->get_text( ).
    RAISE system_error.
  ENDTRY.

ENDFUNCTION.
