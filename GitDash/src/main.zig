const std = @import("std");

const days_visible: usize = 180;
const month_names = [_][]const u8{
    "Oca",
    "Şub",
    "Mar",
    "Nis",
    "May",
    "Haz",
    "Tem",
    "Ağu",
    "Eyl",
    "Eki",
    "Kas",
    "Ara",
};
// const colors = [_][]const u8{
//     "\x1b[48;5;236m", // no activity
//     "\x1b[48;5;23m", // 1 commit
//     "\x1b[48;5;30m", // 2-3 commits
//     "\x1b[48;5;37m", // 4-6 commits
//     "\x1b[48;5;51m", // 7+ commits
// };

const colors = [_][]const u8{
    "\x1b[48;5;236m", // no activity (aynı kaldı - çok koyu gri)
    "\x1b[48;5;94m", // 1 commit (koyu kahve / pas rengi)
    "\x1b[48;5;130m", // 2-3 commits (koyu turuncu)
    "\x1b[48;5;166m", // 4-6 commits (orta turuncu)
    "\x1b[48;5;214m", // 7+ commits (sarımsı parlak turuncu)
};

const Date = struct {
    year: i64,
    month: usize,
    day: usize,
};

pub fn main(init: std.process.Init) !void {
    const allocator = init.gpa;
    const io = init.io;

    var args = init.minimal.args.iterate();
    _ = args.next(); // executable name
    const repo_path: []const u8 = args.next() orelse ".";
    const days_count: usize = if (args.next()) |arg|
        try std.fmt.parseInt(usize, arg, 10)
    else
        days_visible;
    if (days_count == 0 or args.next() != null) {
        std.debug.print("Kullanim: git-activity [repo-yolu] [gun-sayisi]\n", .{});
        return error.InvalidArguments;
    }

    // GNU date is available on Ubuntu/WSL.
    const today_result = try std.process.run(allocator, io, .{
        .argv = &.{ "date", "+%F" },
    });
    defer allocator.free(today_result.stdout);
    defer allocator.free(today_result.stderr);
    if (!today_result.term.success()) return error.DateCommandFailed;

    const start_offset = try allocator.print("{d} days ago", .{days_count - 1});
    defer allocator.free(start_offset);
    const start_result = try std.process.run(allocator, io, .{
        .argv = &.{ "date", "-d", start_offset, "+%F" },
    });
    defer allocator.free(start_result.stdout);
    defer allocator.free(start_result.stderr);
    if (!start_result.term.success()) return error.DateCommandFailed;

    const today_date = std.mem.trim(u8, today_result.stdout, " \t\r\n");
    const start_date = std.mem.trim(u8, start_result.stdout, " \t\r\n");
    const last_day = try parseDay(today_date);
    const first_day = last_day - @as(i64, @intCast(days_count - 1));

    // At local midnight, --since includes the entire first day.
    const since_arg = try allocator.print("--since={s}", .{start_date});
    defer allocator.free(since_arg);

    const git_result = try std.process.run(allocator, io, .{
        .argv = &.{
            "git",          "-C",                           repo_path, "log", since_arg,
            "--format=%cd", "--date=format-local:%Y-%m-%d",
        },
    });
    defer allocator.free(git_result.stdout);
    defer allocator.free(git_result.stderr);

    if (!git_result.term.success()) {
        std.debug.print("Git hatasi: {s}\n", .{git_result.stderr});
        return error.GitCommandFailed;
    }

    const counts = try allocator.alloc(u32, days_count);
    defer allocator.free(counts);
    @memset(counts, 0);
    var total: u64 = 0;
    var lines = std.mem.tokenizeScalar(u8, git_result.stdout, '\n');
    while (lines.next()) |raw_line| {
        const line = std.mem.trim(u8, raw_line, " \t\r");
        const day_number = try parseDay(line);
        if (day_number < first_day or day_number > last_day) continue;

        const index: usize = @intCast(day_number - first_day);
        counts[index] += 1;
        total += 1;
    }

    var output_buffer: [4096]u8 = undefined;
    var file_writer = std.Io.File.stdout().writer(io, &output_buffer);
    const out = &file_writer.interface;
    try render(out, counts, first_day, last_day, total);
    try out.flush();
}

fn render(out: *std.Io.Writer, counts: []const u32, first_day: i64, last_day: i64, total: u64) !void {
    // Sunday = 0, Monday = 1, ... Saturday = 6.
    const first_sunday = first_day - @mod(first_day + 4, 7);
    const week_count: usize = @intCast(@divTrunc(last_day - first_sunday, 7) + 1);

    try out.print("\n  Activity · last {d} days · {d} commits\n\n", .{ counts.len, total });

    // Month labels, one 3-column slot per week.
    try out.writeAll("    ");
    for (0..week_count) |week| {
        const sunday = first_sunday + @as(i64, @intCast(week * 7));
        var month_label: []const u8 = "   ";
        for (0..7) |weekday| {
            const current = sunday + @as(i64, @intCast(weekday));
            if (current < first_day or current > last_day) continue;
            const date = civilFromDays(current);
            if (date.day == 1) month_label = month_names[date.month - 1];
        }
        try out.print("{s}", .{month_label});
    }
    try out.writeAll("\n");

    const labels = [_][]const u8{ "   ", "Pzt", "   ", "Çar", "   ", "Cum", "   " };
    for (0..7) |weekday| {
        try out.print("{s} ", .{labels[weekday]});
        for (0..week_count) |week| {
            const day_number = first_sunday + @as(i64, @intCast(week * 7 + weekday));
            if (day_number < first_day or day_number > last_day) {
                try out.writeAll("   ");
                continue;
            }
            const index: usize = @intCast(day_number - first_day);
            const level = colorLevel(counts[index]);
            try out.print("{s}  \x1b[0m ", .{colors[level]});
        }
        try out.writeAll("\n");
    }

    try out.writeAll("\n    Less ");
    for (colors) |color| {
        try out.print("{s}  \x1b[0m ", .{color});
    }
    try out.writeAll(" More\n\n");
}

fn colorLevel(commits: u32) usize {
    if (commits == 0) return 0;
    if (commits == 1) return 1;
    if (commits <= 3) return 2;
    if (commits <= 6) return 3;
    return 4;
}

fn parseDay(value: []const u8) !i64 {
    if (value.len != 10 or value[4] != '-' or value[7] != '-') return error.InvalidDate;
    const year = try std.fmt.parseInt(i64, value[0..4], 10);
    const month = try std.fmt.parseInt(i64, value[5..7], 10);
    const day = try std.fmt.parseInt(i64, value[8..10], 10);
    if (month < 1 or month > 12 or day < 1 or day > 31) return error.InvalidDate;
    return daysFromCivil(year, month, day);
}

// Howard Hinnant's Gregorian civil-date algorithms.
// Convert a civil date to days since 1970-01-01.
fn daysFromCivil(year: i64, month: i64, day: i64) i64 {
    const y = year - @as(i64, @intFromBool(month <= 2));
    const era = @divFloor(y, 400);
    const yoe = y - era * 400;
    const mp = month + (if (month > 2) @as(i64, -3) else @as(i64, 9));
    const doy = @divTrunc(153 * mp + 2, 5) + day - 1;
    const doe = yoe * 365 + @divTrunc(yoe, 4) - @divTrunc(yoe, 100) + doy;
    return era * 146097 + doe - 719468;
}

// Convert days since 1970-01-01 back to a civil date.
fn civilFromDays(days: i64) Date {
    const z = days + 719468;
    const era = @divFloor(z, 146097);
    const doe = z - era * 146097;
    const yoe = @divTrunc(doe - @divTrunc(doe, 1460) + @divTrunc(doe, 36524) - @divTrunc(doe, 146096), 365);
    var year = yoe + era * 400;
    const doy = doe - (365 * yoe + @divTrunc(yoe, 4) - @divTrunc(yoe, 100));
    const mp = @divTrunc(5 * doy + 2, 153);
    const day = doy - @divTrunc(153 * mp + 2, 5) + 1;
    const month = mp + (if (mp < 10) @as(i64, 3) else @as(i64, -9));
    if (month <= 2) year += 1;
    return .{ .year = year, .month = @intCast(month), .day = @intCast(day) };
}

test "days from civil handles epoch and leap years" {
    try std.testing.expectEqual(@as(i64, 0), daysFromCivil(1970, 1, 1));
    const leap = daysFromCivil(2024, 2, 29);
    const reconstructed = civilFromDays(leap);
    try std.testing.expectEqual(@as(i64, 2024), reconstructed.year);
    try std.testing.expectEqual(@as(usize, 2), reconstructed.month);
    try std.testing.expectEqual(@as(usize, 29), reconstructed.day);
}

test "activity buckets" {
    try std.testing.expectEqual(@as(usize, 0), colorLevel(0));
    try std.testing.expectEqual(@as(usize, 1), colorLevel(1));
    try std.testing.expectEqual(@as(usize, 2), colorLevel(3));
    try std.testing.expectEqual(@as(usize, 3), colorLevel(5));
    try std.testing.expectEqual(@as(usize, 4), colorLevel(7));
}
