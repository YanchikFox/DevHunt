import type { Meta } from "@storybook/react-vite"
import { useForm } from "react-hook-form"
import { Form, FormField, FormItem, FormLabel, FormControl, FormMessage } from "./form"

function FormExample() {
  const form = useForm({ defaultValues: { example: "" } })
  return (
    <Form {...form}>
      <form>
        <FormField
          control={form.control}
          name="example"
          render={({ field }) => (
            <FormItem>
              <FormLabel>Example Field</FormLabel>
              <FormControl>
                <input placeholder="Type something..." {...field} />
              </FormControl>
              <FormMessage />
            </FormItem>
          )}
        />
      </form>
    </Form>
  )
}

const meta = {
  title: "UI/Form",
  component: FormExample,
  parameters: { layout: "centered" },
  tags: ["autodocs"],
} satisfies Meta<typeof FormExample>

export default meta

export const Default = {}
