import { describe, it, expect } from "vitest"
import { render } from "@testing-library/react"
import { Button } from "@/components/ui/button"

describe("Button", () => {
  it("renders with text", () => {
    const { getByText } = render(<Button>Click me</Button>)
    expect(getByText("Click me")).toBeInTheDocument()
  })

  it("renders as disabled", () => {
    const { getByText } = render(<Button disabled>Disabled</Button>)
    expect(getByText("Disabled")).toBeDisabled()
  })

  it("renders with different variants", () => {
    const { getByText, rerender } = render(<Button variant="default">Default</Button>)
    expect(getByText("Default")).toBeInTheDocument()

    rerender(<Button variant="destructive">Destructive</Button>)
    expect(getByText("Destructive")).toBeInTheDocument()
  })

  it("has accessible role", () => {
    const { getByRole } = render(<Button>Accessible</Button>)
    const button = getByRole("button", { name: "Accessible" })
    expect(button).toBeInTheDocument()
  })
})
