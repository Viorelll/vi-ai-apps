import { test, expect } from "@playwright/test";
test("anonymous tutoring with citations and adaptation", async ({ page }) => {
  await page.goto("/");
  await page.getByRole("button", { name: "Începe o sesiune nouă" }).click();
  await expect(
    page.getByText("Mod demonstrativ", { exact: false }),
  ).toBeVisible();
  await page.getByRole("button", { name: "Explică din surse" }).click();
  await expect(page.locator(".explanation")).toBeVisible();
  await expect(page.locator("blockquote a").first()).toHaveAttribute(
    "href",
    /https:\/\/learn.microsoft.com/,
  );
  for (let i = 0; i < 2; i++) {
    const sessionId = await page.evaluate(() =>
      localStorage.getItem("tutorai-session"),
    );
    const previousExerciseId = await page.evaluate(async (id) => {
      if (!id) return null;
      const response = await fetch(`/api/tutor/sessions/${id}`);
      const session = await response.json();
      return session.exercise?.id ?? null;
    }, sessionId);
    await page.getByRole("button", { name: "Generează exercițiu" }).click();
    await page.waitForFunction(
      async ({ id, oldExerciseId }) => {
        if (!id) return false;
        const response = await fetch(`/api/tutor/sessions/${id}`);
        if (!response.ok) return false;
        const session = await response.json();
        return (
          session.state === "ExerciseReady" &&
          session.exercise?.id !== oldExerciseId
        );
      },
      { id: sessionId, oldExerciseId: previousExerciseId },
    );
    await expect(page.getByText(/Nivel \d+ · ExerciseReady/)).toBeVisible();
    await page.getByRole("radio", { name: "Retriever" }).check();
    await expect(page.getByRole("radio", { name: "Retriever" })).toBeChecked();
    await expect(
      page.getByRole("button", { name: "Trimite răspunsul" }),
    ).toBeEnabled();
    await page.getByRole("button", { name: "Trimite răspunsul" }).click();
    await expect(
      page.getByRole("heading", { name: "Feedback · 100%" }),
    ).toBeVisible();
  }
  await expect(page.getByText("Nivelul următor:")).toContainText("2");
  await page.reload();
  await expect(page.getByText("Nivelul următor:")).toContainText("2");
});
