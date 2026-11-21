# CamdiansRX — Guide for the pharmacy

This is for the people who use CamdiansRX at the counter. It covers the four
things you may need to do yourself. Everything else the program does on its own.

Written in English because that is what the screen says. If a word on this page
does not match a word on the screen, trust the screen and tell us.

---

## 1. Starting the program the first time

You will be asked for an **activation code**. We give you that code; type it in
once and you will not be asked again until it expires.

The program needs the internet for this one step. If there is no internet it
will still activate and you can still sell — but it will not send anything to
the server until we connect it. Tell us and we will finish it later.

After that you set up the pharmacy name and the first **Administrator** account.

> **Choose the Administrator password yourselves and keep it.** We do not know
> it and cannot read it. If it is lost, the only way back is the recovery email —
> so set one while you still can: **My Page → Recovery Settings**.

---

## 2. Making a backup

**Admin Dashboard → Data Center → Create Backup File**

This writes one file. That file is everything: products, stock, sales, staff and
the photos. Keep it somewhere other than this computer — a USB stick is enough.
A backup on the same computer does not help when the computer is what fails.

**Make one before anything unusual**: before a reinstall, before someone works
on the computer, before you let the program update after a long time offline.

Two things worth knowing:

- **Restore replaces everything.** It does not merge. Whatever is in the
  program now is gone and the backup takes its place. The program makes its own
  safety copy first, but do not rely on that — make your own.
- **A backup from one computer may not fully work on another.** The sales and
  stock will come across, but the link to our server will not; it is tied to
  this Windows account on this machine. If you move to a new computer, call us
  and we will reconnect it in a few minutes.

---

## 3. Checking that the server is receiving your data

Most days you do not need to. The program sends by itself every 30 minutes and
says nothing when it works.

**When to look:** a warning appears in the top bar of the main screen.

**Where to look:** Admin Dashboard → **Data Center → SERVER SYNC**

| What it says | What it means | What to do |
|---|---|---|
| `Connected.` after **Test Connection** | Everything is fine | Nothing |
| `Automatic sync is off` | Auto is not ticked | Tick **Auto**, or press **Send Data** by hand |
| `Not connected to a server` | The link is missing | Call us |
| `N rows waiting to be sent.` and the number keeps growing | It has not sent for a while | Check the internet, then press **Send Data** |
| `Nothing waiting.` | Everything has been sent | Nothing |

**Nothing is lost while it cannot send.** The program keeps every sale and sends
them when the line comes back. A number that stops growing after you press
Send Data is the sign that it worked.

The warning never appears when there is nothing waiting — a quiet week is not a
fault.

---

## 4. Updates

**You do nothing.** The program checks when it starts, downloads quietly, and
installs the next time you close it. It will not interrupt a sale and it will
not ask you anything.

This means the program may be a slightly different version in the morning than
it was the night before. That is normal.

If something stops working after an update, tell us the date and what you were
doing. There is a log on the computer we can read over the phone —
you do not need to find it yourself, we will tell you where to click.

---

## Things not to do

| | Why |
|---|---|
| Do not delete the folder `CamdiansRX` under your user's **AppData\Roaming** | That is your data. The program folder is elsewhere; deleting that one is safe, deleting this one is not |
| Do not copy the database to another computer by hand | It will open but will not reach the server, and sales may be counted twice. Call us instead |
| Do not run two copies of the program on two computers for the same pharmacy | They do not know about each other |
| Do not share the Administrator account with staff | Give staff their own accounts. The program records who did what, and that only means something if the accounts are separate |

---

## When the program will not open

**"Your licence has expired."** Call us. We can send a renewal code by phone or
message and you type it into the screen that is already in front of you.

You can also renew **before** it expires: a notice appears in the top bar 30 days
ahead, and Admin Dashboard → **License** takes the new code any time. Doing it
early is better — once it expires, the program stops at the counter.

Nothing is lost while it is expired. Your data is untouched and comes back the
moment the code goes in.

---

## What we can see

We receive your product list, stock, sales records, staff names and roles, and
which screens of the program get used. We agreed this with you separately and
you can read what is recorded in the consent document we gave you.

We do **not** receive passwords. They never leave this computer in a readable
form.

Your sales and stock are sent only while **Auto** is ticked, or when someone
presses **Send Data**. That switch is in **Data Center → SERVER SYNC** and you
can look at it any time. We do not receive your staff list at all.

---

## Calling us

Have these ready — it saves a long conversation:

1. **The version**, bottom-left of any screen: `CamdiansRX v.2.1`
2. **What the screen says**, word for word — the exact message matters
3. **What you were doing** when it happened
4. Whether it happens **every time** or happened once
